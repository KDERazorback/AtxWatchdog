using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AtxDataDumper
{
    class Program
    {
        private const byte DATA_PACKET_START = 0x11;
        private const byte STATUS_PACKET_START = 0x12;

        private static bool abortSignalRequested = false;

        private static ulong statusDataLength = 0;
        private static ulong dataDataLength = 0;

        private static FileStream dataStream;
        private static FileStream statusStream;

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

            OpenDataStream();
            OpenStatusStream();

            byte[] buffer = new byte[4096];
            MemoryStream dataStream = new MemoryStream(buffer.Length);
            MemoryStream statusStream = new MemoryStream(buffer.Length);
            StringBuilder str = new StringBuilder(buffer.Length);

            bool isDataPacket = false;
            bool isStatusPacket = false;

            while (port.IsOpen && !abortSignalRequested)
            {
                try
                {
                    while (port.BytesToRead > 0)
                    {
                        int read = port.Read(buffer, 0, buffer.Length);

                        for (int i = 0; i < read; i++)
                        {
                            if (isDataPacket)
                                dataStream.WriteByte(buffer[i]);
                            else if (isStatusPacket)
                                statusStream.WriteByte(buffer[i]);
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

                                if (buffer[i] == 0x0D || buffer[i] == 0x0A)
                                {
                                    // End of line

                                    if (buffer[i] == 0x0A && str.Length < 1)
                                        continue;

                                    ProcessOutputLine(str.ToString());
                                    str.Length = 0;
                                    continue;
                                }
                                
                                // Normal log output
                                str.Append((char)buffer[i]);
                            }

                            if (dataStream.Length >= 8)
                            {
                                // End data packet
                                isDataPacket = false;
                                ProcessDataPacket(ref dataStream);
                            }

                            if (statusStream.Length >= 2)
                            {
                                // End status packet
                                isStatusPacket = false;
                                ProcessStatusPacket(ref statusStream);
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

            CloseDataStream();
            CloseStatusStream();

            Console.WriteLine(statusDataLength.ToString("N0") + " status bytes received.");
            Console.WriteLine(dataDataLength.ToString("N0") + " data bytes received.");

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

        private static void OpenDataStream()
        {
            dataStream = new FileStream(".\\datastream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private static void OpenStatusStream()
        {
            statusStream = new FileStream(".\\statusstream.bin", FileMode.Create, FileAccess.Write, FileShare.None);
        }

        private static void CloseDataStream()
        {
            dataStream.Flush(true);
            dataStream.Dispose();
        }

        private static void CloseStatusStream()
        {
            statusStream.Flush(true);
            statusStream.Dispose();
        }

        private static void ProcessDataPacket(ref MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(dataStream);

            dataDataLength += (ulong)stream.Length;
            stream.SetLength(0);
        }

        private static void ProcessStatusPacket(ref MemoryStream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);
            stream.CopyTo(statusStream);

            statusDataLength += (ulong) stream.Length;
            stream.SetLength(0);
        }

        private static void ProcessOutputLine(string line)
        {
            Console.WriteLine(line);
        }
    }
}
