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
        private const int BAUD_RATE = 2000000; // From Arduino code

        private static bool abortSignalRequested = false;

        private static ulong statusDataLength = 0;
        private static ulong dataDataLength = 0;
        private static ulong metadataDataLength = 0;
        private static bool isPackedProtocolPresent = false;
        private static bool waitDeviceWelcome = true; // Indicates that the port should wait for the device to send
                                                      // an ASCII byte from the Serial line before starting to dump its incoming data on the Data Stream
                                                      // This is used to avoid false readings while the device is powering on due to the main PIC not being
                                                      // connected to the RTS line of the USB-UART IC

        private static string[] BytePowers = { "b", "kib", "mib", "gib", "tib" };
        private static string[] HertzPowers = { "hz", "khz", "mhz", "ghz", "thz" };

        private static FileStream dataStream;
        private static FileStream statusStream;
        private static FileStream rawStream;
        private static FileStream metadataStream;
        private static FileStream textStream;

        private static DateTime lastStatUpdate = DateTime.Now;

        private static ConsoleOverlay statsOverlay = new ConsoleOverlay();
        private static ulong byteCounter = 0;
        private static ulong dataPacketCounter = 0;
        private static bool showStatsOverlay = true;
        private static bool showOverlaySamplingRate = false;
        private static RunningAvg speedAvg = new RunningAvg();
        private static RunningAvg rateAvg = new RunningAvg();

        static void Main(string[] args)
        {
            Console.Title = "ATX Watchdog data dumper";
            SafeWriteLine("ATXWatchdog Serial Data Dumper");
            SafeWriteLine("Copyright (c) Fabian Ramos 2019");
            SafeWriteLine();
            SafeWriteLine("Current working path: {0}", Directory.GetCurrentDirectory());
            SafeWriteLine();
            SafeWriteLine("Press Ctrl+C when done to flush buffers and quit.");
            SafeWriteLine();

            if (args == null || args.Length < 1)
            {
                SafeWriteLine("No serial port specified.");
                SafeWriteLine("Available ports.");
                foreach (string name in SerialPort.GetPortNames())
                    SafeWriteLine("\t" + name);
                SafeWriteLine();
                SafeWriteLine("An input file can be specified instead of a Serial port address, in order to read from it.");
#if DEBUG
                SafeWriteLine();
                SafeWriteLine("Press any key to exit");
                statsOverlay.ClearOverlay();
                Console.ReadKey();
#endif
                return;
            }

            if (args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string parm = args[i].Trim();

                    int parmNameIndex = -1;
                    if (parm.StartsWith("/", StringComparison.Ordinal) || parm.StartsWith("-", StringComparison.Ordinal))
                        parmNameIndex = 1;
                    if (parm.StartsWith("--", StringComparison.Ordinal))
                        parmNameIndex = 2;

                    if (parmNameIndex > 0 && parmNameIndex < parm.Length)
                    {
                        parm = parm.Substring(parmNameIndex);

                        if (string.Equals(parm, "nowelcome", StringComparison.OrdinalIgnoreCase))
                        {
                            SafeWriteLine(" -- Disabling waiting for welcome byte on the Serial stream --");
                            waitDeviceWelcome = false;
                        }

                        if (string.Equals(parm, "nostats", StringComparison.OrdinalIgnoreCase))
                        {
                            SafeWriteLine(" -- Disabling displaying Stream stats --");
                            showStatsOverlay = false;
                        }

                        if (string.Equals(parm, "rate", StringComparison.OrdinalIgnoreCase))
                        {
                            SafeWriteLine(" -- Enabling sampling rate calculation --");
                            showOverlaySamplingRate = true;
                        }
                    }
                }
            }

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            string serialAddress = args[0].Trim();
            bool isUsingSerial = true;
            SerialPort port = null;
            FileStream fs = null;

            bool isUnixDevice = Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX;
            if (isUnixDevice)
                isUnixDevice = serialAddress.StartsWith("/dev/", StringComparison.Ordinal);

            SafeWriteLine("Current platform is: {0}. {1}", Environment.OSVersion.Platform.ToString(), (isUnixDevice ? "UNIX device support enabled." : ""));

            if (File.Exists(serialAddress) && !isUnixDevice)
            {
                try
                {
                    isUsingSerial = false;
                    SafeWriteLine("Reading from local file stream: " + serialAddress);
                    fs = new FileStream(serialAddress, FileMode.Open, FileAccess.Read, FileShare.Read);
                    SafeWriteLine("File opened.");
                }
                catch (Exception e)
                {
                    SafeWriteLine("Cannot open file. Press any key to exit");
                    SafeWriteLine(e);
#if DEBUG
                    SafeWriteLine();
                    SafeWriteLine("Press any key to exit");
                    statsOverlay.ClearOverlay();
                    Console.ReadKey();
#endif
                    return;
                }
            }
            else
            {
                if (!isUnixDevice) serialAddress = serialAddress.ToUpperInvariant();
                SafeWriteLine("Reading from Serial port: {0} at a baud rate of {1}", serialAddress.ToUpperInvariant(), BAUD_RATE.ToString("N0"));
                port = new SerialPort(serialAddress, BAUD_RATE, Parity.None, 8, StopBits.One);
                port.DtrEnable = true;
                port.RtsEnable = false;

                try
                {
                    port.Open();
                    SafeWriteLine("Port opened.");
                }
                catch (Exception e)
                {
                    SafeWriteLine("Cannot open serial port. Press any key to exit");
                    SafeWriteLine(e);
#if DEBUG
                    SafeWriteLine();
                    SafeWriteLine("Press any key to exit");
                    statsOverlay.ClearOverlay();
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
            ulong discardedPreWelcomeByteCount = 0;

            bool isDataPacket = false;
            bool isStatusPacket = false;
            bool isMetadataPacket = false;
            bool didDeviceWelcome = !waitDeviceWelcome; // Indicates if the device already sent an ASCII byte <0x7F down the Serial line
            int metadataLength = 0;

            while ((!isUsingSerial || port.IsOpen) && !abortSignalRequested)
            {
                UpdateStats();

                try
                {
                    while ((!isUsingSerial || port.BytesToRead > 0) && !abortSignalRequested)
                    {
                        UpdateStats();

                        int read;
                        if (isUsingSerial)
                        {
                            read = port.Read(buffer, 0, buffer.Length);
                            rawStream.Write(buffer, 0, read);
                        }
                        else
                            read = fs.Read(buffer, 0, buffer.Length);

                        byteCounter += (ulong)read;

                        for (int i = 0; i < read; i++)
                        {
                            if (didDeviceWelcome)
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
                            } else
                            {
                                if (buffer[i] < 0x7F)
                                {
                                    didDeviceWelcome = true;
                                    // Normal log output
                                    str.Append((char)buffer[i]);
                                }
                                else
                                {
                                    //discard input byte
                                    discardedPreWelcomeByteCount++;
                                }
                            }

                            if (dataStream.Length >= 8)
                            {
                                // End data packet
                                isDataPacket = false;
                                ProcessDataPacket(ref dataStream, isPackedProtocolPresent);
                                dataPacketCounter++;
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
                    SafeWriteLine("An exception occurred on the main loop.");
                    SafeWriteLine(e);
                    statsOverlay.ClearOverlay();
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

            if (metadataStream.Position > 0 && metadataStream.Length > 0)
                metadataStream.SetLength(metadataStream.Position);

            dataStream.Dispose();
            statusStream.Dispose();
            metadataStream.Dispose();

            CloseStreams();

            SafeWriteLine(statusDataLength.ToString("N0") + " status bytes received.");
            SafeWriteLine(dataDataLength.ToString("N0") + " data bytes received.");
            SafeWriteLine(metadataDataLength.ToString("N0") + " metadata bytes received.");
            SafeWriteLine(discardedPreWelcomeByteCount.ToString("N0") + " discarded bytes prior to device welcome.");

            if (showStatsOverlay) SafeWriteLine(SizeToHumanReadable(speedAvg.Mean) + " mean transfer speed.");
            if (showOverlaySamplingRate) SafeWriteLine(SizeToHumanReadable(rateAvg.Mean, HertzPowers, 1000.0f) + " mean sampling rate.");

#if DEBUG
            SafeWriteLine();
            SafeWriteLine("Press any key to exit");
            statsOverlay.ClearOverlay();
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
            SafeWriteLine(line);
        }

        private static void SafeWriteLine()
        {
            statsOverlay.ClearOverlay();
            Console.WriteLine();
            statsOverlay.ShowOverlay();
        }

        private static void SafeWriteLine(string message)
        {
            statsOverlay.ClearOverlay();
            Console.WriteLine(message);
            statsOverlay.ShowOverlay();
        }

        private static void SafeWriteLine(string message, params string[] args)
        {
            statsOverlay.ClearOverlay();
            Console.WriteLine(message, args);
            statsOverlay.ShowOverlay();
        }

        private static void SafeWriteLine(Exception e)
        {
            statsOverlay.ClearOverlay();
            Console.WriteLine(e);
            statsOverlay.ShowOverlay();
        }

        private static string SizeToHumanReadable(double size, string[] powers = null, float divisor = 1024.0f)
        {
            if (powers == null) powers = BytePowers;
            int index = 0;

            while (size > divisor)
            {
                size /= divisor;
                index++;

                if (index == powers.Length - 1) break;
            }

            return size.ToString("N1") + powers[index];
        }

        private static string SizeToHumanReadable(long size)
        {
            return SizeToHumanReadable((double)size);
        }

        private static void UpdateStats()
        {
            if (!showStatsOverlay)
            {
                byteCounter = 0;
                dataPacketCounter = 0;
                return;
            }

            TimeSpan delta = DateTime.Now - lastStatUpdate;
            if (delta.TotalMilliseconds > 1000)
            {
                float speed = (float)((byteCounter * 1000) / delta.TotalMilliseconds);
                float rate = (float)((dataPacketCounter * 1000) / delta.TotalMilliseconds);

                speedAvg.Add(speed);
                rateAvg.Add(rate);

                string message = string.Format("Rx: {0} | Now: {1}/s  ",
                    SizeToHumanReadable(dataDataLength),
                    SizeToHumanReadable(speed));

                if (showOverlaySamplingRate)
                    message += string.Format("@ {0} ", SizeToHumanReadable(rate, HertzPowers, 1000.0f));

                statsOverlay.ShowOverlay(message);
                lastStatUpdate = DateTime.Now;
                byteCounter = 0;
                dataPacketCounter = 0;
            }
        }
    }
}
