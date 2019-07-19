using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace AtxDataDumper
{
    class Program
    {
        // Note: In Packed protocol mode, status data is sent along with a timestamp and rail data in 8 bytes.

        private const byte DATA_PACKET_START = 0x11; // From Arduino code
        private const byte STATUS_PACKET_START = 0x12; // From Arduino code
        private const byte METADATA_PACKET_START = 0x13; // From Arduino code

        private static bool abortSignalRequested = false;

        private static ulong statusDataLength = 0;
        private static ulong dataDataLength = 0;
        private static ulong metadataDataLength = 0;
        private static bool isPackedProtocolPresent = false;

        private static FileStream dataStream;
        private static FileStream statusStream;
        private static FileStream rawStream;
        private static FileStream metadataStream;
        private static FileStream textStream;

        static void Main(string[] args)
        {
            Console.Title = "ATX Watchdog data dumper";
            Console.WriteLine("ATXWatchdog Serial Data Dumper");
            Console.WriteLine("Copyright (c) Fabian Ramos 2019");
            Console.WriteLine();
            Console.WriteLine("Current working path: {0}", Directory.GetCurrentDirectory());
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C when done to flush buffers and quit.");
            Console.WriteLine();

            if (args == null || args.Length < 1)
            {
                Console.WriteLine("No serial port specified.");
                Console.WriteLine("Available ports.");
                foreach (string name in SerialPort.GetPortNames())
                    Console.WriteLine("\t" + name);
                Console.WriteLine();
                Console.WriteLine("An input file can be specified instead of a Serial port address, in order to read from it.");
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            string serialAddress = args[0].Trim();
            bool isUsingSerial = true;
            SerialPort port = null;
            FileStream fs = null;

            bool isUnixDevice = Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX;
            if (isUnixDevice)
                isUnixDevice = serialAddress.StartsWith("/dev/", StringComparison.Ordinal);

            if (File.Exists(serialAddress) && !isUnixDevice)
            {
                try
                {
                    isUsingSerial = false;
                    Console.WriteLine("Reading from local file stream: " + serialAddress);
                    fs = new FileStream(serialAddress, FileMode.Open, FileAccess.Read, FileShare.Read);
                    Console.WriteLine("File opened.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Cannot open file. Press any key to exit");
                    Console.WriteLine(e);
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                    return;
                }
            }
            else
            {
                if (!isUnixDevice) serialAddress = serialAddress.ToUpperInvariant();
                Console.WriteLine("Reading from Serial port: " + serialAddress.ToUpperInvariant());
                port = new SerialPort(serialAddress, 2000000, Parity.None, 8, StopBits.One);
                port.DtrEnable = true;
                port.RtsEnable = false;

                try
                {
                    port.Open();
                    Console.WriteLine("Port opened.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("Cannot open serial port. Press any key to exit");
                    Console.WriteLine(e);
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                    return;
                }
            }

            OpenStreams(isUsingSerial);

            byte[] buffer = new byte[4096];
            MemoryStream dataStream = new MemoryStream(buffer.Length);
            MemoryStream statusStream = new MemoryStream(buffer.Length);
            MemoryStream metadataStream = new MemoryStream(buffer.Length);
            StringBuilder str = new StringBuilder(buffer.Length);

            bool isDataPacket = false;
            bool isStatusPacket = false;
            bool isMetadataPacket = false;
            int metadataLength = 0;

            while ((!isUsingSerial || port.IsOpen) && !abortSignalRequested)
            {
                try
                {
                    while ((!isUsingSerial || port.BytesToRead > 0) && !abortSignalRequested)
                    {
                        int read;
                        if (isUsingSerial)
                        {
                            read = port.Read(buffer, 0, buffer.Length);
                            rawStream.Write(buffer, 0, read);
                        }
                        else
                            read = fs.Read(buffer, 0, buffer.Length);


                        for (int i = 0; i < read; i++)
                        {
                            if (isDataPacket)
                                dataStream.WriteByte(buffer[i]);
                            else if (isStatusPacket)
                                statusStream.WriteByte(buffer[i]);
                            else if (isMetadataPacket)
                            {
                                if (metadataLength == 0)
                                    metadataLength = buffer[i];
                                else
                                    metadataStream.WriteByte(buffer[i]);
                            }
                            else
                            {
                                if (buffer[i] == DATA_PACKET_START)
                                {
                                    // Begin data packet
                                    dataStream.Seek(0, SeekOrigin.Begin);
                                    isDataPacket = true;
                                    continue;
                                }

                                if (buffer[i] == STATUS_PACKET_START)
                                {
                                    // Begin status packet
                                    statusStream.Seek(0, SeekOrigin.Begin);
                                    isStatusPacket = true;
                                    continue;
                                }

                                if (buffer[i] == METADATA_PACKET_START)
                                {
                                    // Begin metadata packet
                                    metadataStream.Seek(0, SeekOrigin.Begin);
                                    metadataLength = 0;
                                    isMetadataPacket = true;
                                    continue;
                                }

                                if (buffer[i] == 0x0D || buffer[i] == 0x0A)
                                {
                                    // End of line

                                    if (buffer[i] == 0x0A && str.Length < 1)
                                        continue;

                                    ProcessOutputLine(str.ToString());
                                    str.Length = 0;
                                    continue;
                                }

                                if (buffer[i] >= 0x80)
                                {
                                    // Start of new packed protocol for data packets
                                    dataStream.Seek(0, SeekOrigin.Begin);
                                    dataStream.WriteByte(buffer[i]);
                                    isDataPacket = true;
                                    isPackedProtocolPresent = true;
                                    continue;
                                }
                                
                                // Normal log output
                                str.Append((char)buffer[i]);
                            }

                            if (dataStream.Length >= 8)
                            {
                                // End data packet
                                isDataPacket = false;
                                ProcessDataPacket(ref dataStream, isPackedProtocolPresent);
                            }

                            if (statusStream.Length >= 2)
                            {
                                // End status packet
                                isStatusPacket = false;
                                ProcessStatusPacket(ref statusStream, false);
                            }

                            if (metadataStream.Length >= metadataLength)
                            {
                                // End metadata packet
                                isMetadataPacket = false;
                                ProcessMetadataPacket(ref metadataStream);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("An exception occurred on the main loop.");
                    Console.WriteLine(e);
                    break;
                }
            }

            if (isUsingSerial)
            {
                Thread t = new Thread(() =>
                {
                    if (port.IsOpen)
                    {
                        port.DtrEnable = false;
                        port.DiscardInBuffer();
                        port.DiscardOutBuffer();
                        port.Close();
                    }
                });
                t.Start();
                t.Join();
            }
            else
            {
                fs.Close();
                fs.Dispose();
            }
            

            dataStream.Dispose();
            statusStream.Dispose();
            metadataStream.Dispose();

            CloseStreams();

            Console.WriteLine(statusDataLength.ToString("N0") + " status bytes received.");
            Console.WriteLine(dataDataLength.ToString("N0") + " data bytes received.");
            Console.WriteLine(metadataDataLength.ToString("N0") + " metadata bytes received.");

#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
        }

        private static void ConsoleOnCancelKeyPress(object sender, ConsoleCancelEventArgs consoleCancelEventArgs)
        {
            abortSignalRequested = true;
            consoleCancelEventArgs.Cancel = true;
        }

        private static void OpenStreams(bool openRawStream)
        {
            dataStream = new FileStream("." + Path.DirectorySeparatorChar + "datastream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            statusStream = new FileStream("." + Path.DirectorySeparatorChar + "statusstream.bin", FileMode.Create, FileAccess.Write, FileShare.None);

            if (openRawStream)
                rawStream = new FileStream("." + Path.DirectorySeparatorChar + "rawstream.bin", FileMode.Create, FileAccess.Write, FileShare.None);

            metadataStream = new FileStream("." + Path.DirectorySeparatorChar + "metadatastream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            textStream = new FileStream("." + Path.DirectorySeparatorChar + "textstream.txt", FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private static void CloseStreams()
        {
            dataStream.Flush(true);
            dataStream.Dispose();

            if (rawStream != null)
            {
                rawStream.Flush(true);
                rawStream.Dispose();
            }

            statusStream.Flush(true);
            statusStream.Dispose();

            metadataStream.Flush(true);
            metadataStream.Dispose();

            textStream.Flush(true);
            textStream.Dispose();
        }

        private static void ProcessDataPacket(ref MemoryStream stream, bool packedProtocol)
        {
            stream.Seek(0, SeekOrigin.Begin);

            if (packedProtocol)
            {
                // 8 bytes protocol length
                byte[] buffer = new byte[8];

                if (stream.Length % 8 != 0 || stream.Read(buffer, 0, buffer.Length) != 8)
                    throw new DataMisalignedException("Input data is not aligned properly");

                // Reassemble time-offset data
                byte offset = 0;
                offset |= (byte)(buffer[0] & 0b11100000); // 3 bits from v5
                offset |= (byte)((buffer[2] & 0b10000000) >> 3); // 1 bit  from v5sb
                offset >>= 1; // Align data
                if (offset == 0)
                    offset = 128; // Overflow

                dataStream.WriteByte(offset); // Write offset

                dataStream.WriteByte((byte) (buffer[0] & 0b00111111)); // Hi
                dataStream.WriteByte(buffer[1]); // Lo
                dataStream.WriteByte((byte) (buffer[2] & 0b00011111)); // Hi
                dataStream.WriteByte(buffer[3]); // Lo
                dataStream.WriteByte((byte) (buffer[4] & 0b00011111)); // Hi
                dataStream.WriteByte(buffer[5]); // Lo
                dataStream.WriteByte((byte) (buffer[6] & 0b00011111)); // Hi
                dataStream.WriteByte(buffer[7]); // Lo

                dataDataLength += 8 + 1; // 8 data bytes written + 1 time offset byte written

                ProcessStatusPacket(ref stream, true);
            }
            else
            {
                stream.CopyTo(dataStream);
                dataDataLength += (ulong)stream.Length;
            }

            stream.SetLength(0);
        }

        private static void ProcessStatusPacket(ref MemoryStream stream, bool packedProtocol)
        {
            stream.Seek(0, SeekOrigin.Begin);

            if (packedProtocol)
            {
                byte b1 = 0;
                /* B1 mapping in output data
                 * b7 -- PS_ON present      --- MSB
                 * b6 -- PWR_OK present
                 * b5 -- Buzzer is beeping
                 * b4 -- Reserved. Always 1
                 * b3 -- Reserved. Always 1
                 * b2 -- PSU_MODE
                 * b1 -- PSU_OK_ACTIVE
                 * b0 -- ATX_IS_TRIGGERED   --- LSB
                 */

                byte input = (byte) stream.ReadByte(); // V12 WORD
                stream.ReadByte(); // Discard 1 byte

                if ((input & 0b01000000) > 0) b1 |= 0b00000100; // Board PSU_MODE

                stream.ReadByte(); // DISCARD V5 ENTIRELY
                stream.ReadByte();

                input = (byte) stream.ReadByte(); // V5SB WORD
                stream.ReadByte(); // Discard 1 byte

                if ((input & 0b01000000) > 0) b1 |= 0b00000010; // PSU_OK_ACTIVE
                if ((input & 0b00100000) > 0) b1 |= 0b00000001; // ATX_IS_TRIGGERED

                input = (byte) stream.ReadByte(); // V3_3 H-WORD

                if ((input & 0b10000000) > 0) b1 |= 0b01000000; // PWR_OK Present
                if ((input & 0b01000000) > 0) b1 |= 0b10000000; // PS_ON Present
                if ((input & 0b00100000) > 0) b1 |= 0b00100000; // Buzzer is beeping

                // Fill reserved bytes
                b1 |= 0b00011000;

                statusStream.WriteByte(b1);

                statusDataLength += 1;
            }
            else
            {
                stream.CopyTo(statusStream);
                statusDataLength += (ulong) stream.Length;
            }

            stream.SetLength(0);
        }

        private static void ProcessMetadataPacket(ref MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            stream.CopyTo(metadataStream);
            metadataDataLength += (ulong)stream.Length;
            stream.SetLength(0);
        }

        private static void ProcessOutputLine(string line)
        {
            if (textStream.CanWrite)
            {
                byte[] buffer = Encoding.ASCII.GetBytes(line + "\n");
                textStream.Write(buffer, 0, buffer.Length);
            }
            Console.WriteLine(line);
        }
    }
}
