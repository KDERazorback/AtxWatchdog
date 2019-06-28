using System;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace AtxDataDumper
{
    class Program
    {
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
            Console.WriteLine("Press Ctrl+C when done to flush buffers and quit.");

            if (args == null || args.Length < 1)
            {
                Console.WriteLine("No serial port specified.");
                Console.WriteLine("Available ports.");
                foreach (string name in SerialPort.GetPortNames())
                    Console.WriteLine("\t" + name);
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            string serialAddress = args[0].Trim().ToUpperInvariant();

            SerialPort port = new SerialPort(serialAddress, 115200, Parity.None, 8, StopBits.One);
            port.DtrEnable = true;
            port.RtsEnable = false;

            try
            {
                port.Open();
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

            Console.WriteLine("Port opened.");

            OpenStreams();

            byte[] buffer = new byte[4096];
            MemoryStream dataStream = new MemoryStream(buffer.Length);
            MemoryStream statusStream = new MemoryStream(buffer.Length);
            MemoryStream metadataStream = new MemoryStream(buffer.Length);
            StringBuilder str = new StringBuilder(buffer.Length);

            bool isDataPacket = false;
            bool isStatusPacket = false;
            bool isMetadataPacket = false;

            while (port.IsOpen && !abortSignalRequested)
            {
                try
                {
                    while (port.BytesToRead > 0)
                    {
                        int read = port.Read(buffer, 0, buffer.Length);
                        rawStream.Write(buffer, 0, read);

                        for (int i = 0; i < read; i++)
                        {
                            if (isDataPacket)
                                dataStream.WriteByte(buffer[i]);
                            else if (isStatusPacket)
                                statusStream.WriteByte(buffer[i]);
                            else if (isMetadataPacket)
                                metadataStream.WriteByte(buffer[i]);
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

                            if (metadataStream.Length >= 16)
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

        private static void OpenStreams()
        {
            dataStream = new FileStream(".\\datastream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            statusStream = new FileStream(".\\statusstream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            rawStream = new FileStream(".\\rawstream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            metadataStream = new FileStream(".\\metadatastream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
            textStream = new FileStream(".\\textstream.txt", FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private static void CloseStreams()
        {
            dataStream.Flush(true);
            dataStream.Dispose();
            
            rawStream.Flush(true);
            rawStream.Dispose();

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

                if (stream.Length < 8)
                    throw new DataMisalignedException("Input data is not aligned properly");

                dataStream.WriteByte((byte) (stream.ReadByte() & 0b00111111)); // Hi
                dataStream.WriteByte((byte) stream.ReadByte()); // Lo
                dataStream.WriteByte((byte) (stream.ReadByte() & 0b00011111)); // Hi
                dataStream.WriteByte((byte) stream.ReadByte()); // Lo
                dataStream.WriteByte((byte) (stream.ReadByte() & 0b00011111)); // Hi
                dataStream.WriteByte((byte) stream.ReadByte()); // Lo
                dataStream.WriteByte((byte) (stream.ReadByte() & 0b00011111)); // Hi
                dataStream.WriteByte((byte) stream.ReadByte()); // Lo

                dataDataLength += 8;

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
                byte b2 = 0;

                byte input = (byte) stream.ReadByte(); // V12 WORD
                stream.ReadByte(); // Discard 1 byte

                if ((input & 0b01000000) > 0) b2 = 1;

                input = (byte) stream.ReadByte(); // V5 WORD
                stream.ReadByte(); // Discard 1 byte

                if ((input & 0b10000000) > 0) b1 |= 0b00100000; // V12_OOS
                if ((input & 0b01000000) > 0) b1 |= 0b00010000; // V5_OOS
                if ((input & 0b00100000) > 0) b1 |= 0b00000100; // V3_3_OOS

                input = (byte) stream.ReadByte(); // V5SB WORD
                stream.ReadByte(); // Discard 1 byte

                if ((input & 0b10000000) > 0) b1 |= 0b00001000; // V5SB_OOS
                if ((input & 0b01000000) > 0) b1 |= 0b00000010; // PSU_OK_ACTIVE
                if ((input & 0b00100000) > 0) b1 |= 0b00000001; // ATX_IS_TRIGGERED

                input = (byte) stream.ReadByte(); // V3_3 H-WORD

                if ((input & 0b10000000) > 0) b1 |= 0b01000000; // PWR_OK Present
                if ((input & 0b01000000) > 0) b1 |= 0b10000010; // PS_ON Present
                if ((input & 0b00100000) > 0) b2 |= 0b01000000; // Buzzer is beeping

                // Fill reserved bytes
                b2 |= 0b00111111;

                statusStream.WriteByte(b1);
                statusStream.WriteByte(b2);

                statusDataLength += 2;
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
