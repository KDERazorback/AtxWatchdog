using System;
using System.IO;
using System.IO.Ports;
using System.Text;

namespace AtxDfuTool
{
    class MainClass
    {
        private static byte dfuCode = 0xEA; // 234
        private static DfuProgramCodes programCode = DfuProgramCodes.None;
        private const int BAUD_RATE = 2000000;

        private static bool abortSignalRequested = false;

        public static void Main(string[] args)
        {
            Console.Title = "ATX Watchdog DFU tool";
            Console.WriteLine("ATXWatchdog DFU Mode Tool");
            Console.WriteLine("Copyright (c) Fabian Ramos 2019");
            Console.WriteLine();
            Console.WriteLine("Current working path: {0}", Directory.GetCurrentDirectory());
            Console.WriteLine();
            Console.WriteLine("Press Ctrl+C when done to quit.");
            Console.WriteLine();

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
                        string value = "";
                        parm = parm.Substring(parmNameIndex);
                        if (parm.Contains("="))
                        {
                            string[] segments = parm.Split(new char [] { '=' }, 2);
                            parm = segments[0];
                            value = segments[1];
                        }

                        if (string.Equals(parm, "dfucode", StringComparison.OrdinalIgnoreCase))
                        {
                            int v;
                            if (!int.TryParse(value, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out v))
                            {
                                Console.WriteLine("Error: Cannot parse specified HEX value " + value);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                            if (v > 255)
                            {
                                Console.WriteLine("Error: Specified HEX number is out of bounds. ({0})", v);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                            Console.WriteLine(" -- Setting custom DFU magic code to {0} --", value);
                            dfuCode = (byte)v;
                        }

                        if (string.Equals(parm, "programcode", StringComparison.OrdinalIgnoreCase))
                        {
                            DfuProgramCodes v;
                            if (!Enum.TryParse(value, out v))
                            {
                                Console.WriteLine("Error: Cannot parse specified ENUM value " + value);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                            Console.WriteLine(" -- Setting custom PROGRAM code to {0} --", v.ToString());
                            programCode = v;
                        }
                    }
                }
            }

            Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            string serialAddress = args[0].Trim();
            SerialPort port = null;

            bool isUnixDevice = Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX;

            Console.WriteLine("Current platform is: {0}. {1}", Environment.OSVersion.Platform.ToString(), (isUnixDevice ? "UNIX device support enabled." : ""));

            if (!isUnixDevice) serialAddress = serialAddress.ToUpperInvariant();
            Console.WriteLine("Reading from Serial port: {0} at a baud rate of {1}", serialAddress.ToUpperInvariant(), BAUD_RATE.ToString("N0"));
            port = new SerialPort(serialAddress, BAUD_RATE, Parity.None, 8, StopBits.One);
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

            byte remote_protocolVersion = 0;
            StatusCode remote_status = StatusCode.None;
            CircularBuffer<char> cb1 = new CircularBuffer<char>(3);

            // Send DFU code
            DateTime startTime = DateTime.Now;
            bool inSync = false;
            while (!inSync)
            {
                if (abortSignalRequested)
                {
                    port.Close();
                    port.Dispose();
                    Console.WriteLine("Connection closed.");
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                }

                if ((DateTime.Now - startTime).TotalSeconds > 10)
                {
                    port.Close();
                    port.Dispose();
                    Console.WriteLine("Error: No response from the Serial board. Operation timed out.");
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                    return;
                }

                port.Write(new byte[] { dfuCode }, 0, 1);
                System.Threading.Thread.Sleep(25);

                while (port.BytesToRead > 1)
                {
                    if (abortSignalRequested)
                    {
                        port.Close();
                        port.Dispose();
                        Console.WriteLine("Connection closed.");
#if DEBUG
                        Console.WriteLine();
                        Console.WriteLine("Press any key to exit");
                        Console.ReadKey();
#endif
                    }

                    byte[] buffer = new byte[4096];

                    int read = port.Read(buffer, 0, buffer.Length);

                    for (int i = 0; i < read; i++)
                    {
                        cb1.InsertBackwards((char)buffer[i]);

                        if (cb1.ElementAt(0) == 'D' && cb1.ElementAt(1) == 'F' && cb1.ElementAt(2) == 'U')
                        {
                            inSync = true;

                            int b;

                            if (i + 1 < read) b = buffer[i + 1]; else b = port.ReadByte();

                            if (b != 0x01)
                            {
                                port.Close();
                                port.Dispose();
                                Console.WriteLine("Serial I/O error. Unsupported protocol version received.");
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }

                            remote_protocolVersion = (byte)b;

                            if (i + 2 < read) b = buffer[i + 2]; else b = port.ReadByte();

                            if (b < 0)
                            {
                                port.Close();
                                port.Dispose();
                                Console.WriteLine("Serial I/O error. Unexpected end of stream.");
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }

                            remote_status = (StatusCode)((byte)b);

                            break;
                        }
                        if (inSync) break;
                    }
                    if (inSync) break;
                }
            }

            // Device in sync
            Console.WriteLine("Successfully entered DFU mode.");
            Console.WriteLine("Remote Protocol Verison: " + remote_protocolVersion.ToString("X2"));
            Console.WriteLine("Waiting for device ready status...");

            // Wait for device Ready status
            while (remote_status != StatusCode.Ready)
            {
                if (abortSignalRequested)
                {
                    port.Close();
                    port.Dispose();
                    Console.WriteLine("Connection closed.");
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                }

                if (remote_status == StatusCode.Terminated)
                {
                    port.Close();
                    port.Dispose();
                    Console.WriteLine("Remote device has terminated DFU mode connection.");
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                }

                if (port.BytesToRead > 0)
                {
                    int b = port.ReadByte();
                    if (b < 0) continue;

                    if (b >= (byte)StatusCode.None)
                    {
                        StatusCode code = (StatusCode)b;

                        if (code != remote_status)
                        {
                            Console.WriteLine("Device status changed from {0} --to--> {1}", remote_status.ToString(), code.ToString());
                            remote_status = code;
                        }
                    }
                }
            }

            // Flush in buffer
            while (port.BytesToRead > 0)
            {
                byte[] buffer = new byte[4096];
                port.Read(buffer, 0, buffer.Length);
            }

            // Send program code
            Console.WriteLine("Sending program code " + programCode.ToString("X"));
            port.Write(new byte[] { (byte)programCode }, 0, 1);

            // Enter DFU console
            StringBuilder lineBuffer = new StringBuilder();
            while (!abortSignalRequested)
            {
                if (port.BytesToRead > 0)
                {
                    int b = port.ReadByte();
                    if (b < 0) continue;

                    byte val = (byte)b;

                    // Check if the byte is an status code
                    if (val >= (byte)StatusCode.None)
                    {
                        StatusCode code = (StatusCode)val;
                        Console.WriteLine("Device status changed from {0} --to--> {1}", remote_status.ToString(), code.ToString());
                        remote_status = (StatusCode)code;
                        continue;
                    }

                    // Check if the byte is a control byte
                    if (val < 0x20)
                    {
                        switch (val)
                        {
                            case 0x0D:
                                Console.WriteLine(lineBuffer.ToString());
                                lineBuffer.Clear();
                                break;
                            case 0x11: // Clear console screen
                                Console.Clear();
                                break;
                        }
                    } else {
                        if (val < 0x7F) lineBuffer.Append((char)val);
                    }
                }

                if (remote_status == StatusCode.Terminated)
                {
                    port.Close();
                    port.Dispose();
                    Console.WriteLine("Remote device has terminated DFU mode connection.");
#if DEBUG
                    Console.WriteLine();
                    Console.WriteLine("Press any key to exit");
                    Console.ReadKey();
#endif
                    return;
                }
            }

            port.Close();
            port.Dispose();
            Console.WriteLine("Connection closed.");
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
    }
}
