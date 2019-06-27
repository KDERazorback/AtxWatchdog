using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using AtxCsvPlotter.Endpoints;

namespace AtxCsvPlotter
{
    class Program
    {
        private static Dictionary<string, Type> availableModes = new Dictionary<string, Type>()
        {
            { "png", typeof(Endpoints.Png.PngPlotOutput) },
        };

        static void Main(string[] args)
        {
            if (args == null || args.Length < 2)
            {
                Console.WriteLine("Plots CSV files coming from watchdog boards to images or the display");
                Console.WriteLine("Usage:");
                Console.WriteLine("       atxcsvplot.exe <png> <file1.csv> ... [fileN.csv]");
                Console.WriteLine();
                Console.WriteLine(" png");
                Console.WriteLine("   Plots the specified CSV files to multiple windows");
                Console.WriteLine();
                Console.WriteLine("Available output modes");
                foreach (string k in availableModes.Keys)
                    Console.WriteLine("\t" + k);
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            string modeStr = args[0];
            string[] files = new string[args.Length - 1];
            Array.ConstrainedCopy(args, 1, files, 0, files.Length);
            Endpoint modeHandler = null;

            foreach (KeyValuePair<string, Type> endpoint in availableModes)
            {
                if (string.Equals(endpoint.Key, modeStr, StringComparison.OrdinalIgnoreCase))
                {
                    modeHandler = (Endpoint)Activator.CreateInstance(endpoint.Value);
                    break;
                }
            }

            if (modeHandler == null)
            {
                Console.WriteLine("No available mode found with key \"{0}\"", modeStr);
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            // RUNNING CONFIG STARTS HERE
            modeHandler.AxesNames = new string[] {"TENSIÓN", "TIEMPO"};
            modeHandler.LegendBackground = Color.Transparent;
            modeHandler.Background = Color.Transparent;
            modeHandler.Dpi = 300;
            // END OF RUNNING CONFIG

            modeHandler.Initialize();

            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                FileInfo fi = new FileInfo(file);
                if (!fi.Exists)
                {
                    Console.WriteLine("Warning!: Missing file \"{0}\"", file);
                    continue;
                }
                if (fi.Length < 1)
                {
                    Console.WriteLine("Warning!: Empty file \"{0}\"", file);
                    continue;
                }

                string outputFilename = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) +
                                        modeHandler.DefaultExtension;

                Console.WriteLine("Plotting file {0}/{1}  {2}...", (i+1).ToString("N0"), files.Length.ToString("N0"), fi.Name);
                modeHandler.Plot(fi.FullName, outputFilename);
            }

            Console.WriteLine("Plot complete.");
            modeHandler.Finish();

#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
        }
    }
}
