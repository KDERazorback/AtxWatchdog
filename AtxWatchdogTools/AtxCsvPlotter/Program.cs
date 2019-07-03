using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using AtxCsvPlotter.Endpoints;

namespace AtxCsvPlotter
{
    class Program
    {
        private const string RUN_FILENAME = "atxcsvplot.run";

        private static Dictionary<string, Type> availableModes = new Dictionary<string, Type>()
        {
            { "png", typeof(Endpoints.Png.PngPlotOutput) },
        };

        private static Color? PlotBackgroundColor = Color.Transparent;
        private static int? PlotDpi = 150;
        private static bool PlotMetadata = true;
        private static string[] AxisNames = null;
        private static bool LoadRunFile = true;
        private static string ModeName = null;
        private static List<string> InputFiles = new List<string>();
        private static List<string> MetadataFiles = new List<string>();
        private static string GenConfigFilename = null;
        private static string ConfigFilename = null;

        static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Console.WriteLine("Plots CSV files coming from watchdog boards to images or the display");
                Console.WriteLine("Usage:");
                Console.WriteLine("       atxcsvplot.exe png <file1.csv> ... [fileN.csv] [/background=#aarrggbb] [/nometadata] [/dpi=xxx]");
                Console.WriteLine("                          [/axes=<name1>,<name2>] [/norun] [/metadata=[file1],[file2]...[fileN]");
                Console.WriteLine();
                Console.WriteLine(" png");
                Console.WriteLine("   Plots the specified CSV files to png images");
                Console.WriteLine(" /background=#aarrggbb");
                Console.WriteLine("   Sets the background of the rendered color to the specified ARGB value");
                Console.WriteLine(" /nometadata");
                Console.WriteLine("   Skips automatic metadata loading for input files. Metadata are files with the same name, but extension .meta");
                Console.WriteLine(" /dpi=xxx");
                Console.WriteLine("   Sets the DPI of the rendered plot to the specified value, in Dots per Inch");
                Console.WriteLine(" /dpi=xxx");
                Console.WriteLine("   Sets the DPI of the rendered plot to the specified value, in Dots per Inch");
                Console.WriteLine(" /axes=name1,name2");
                Console.WriteLine("   Sets the name of each Axis on the rendered plot. The first value is the vertical axis, the second one the horizontal");
                Console.WriteLine(" /norun");
                Console.WriteLine("   Ignores any " + RUN_FILENAME + " file present on the current working directory.");
                Console.WriteLine(" /metadata=file1;file2;...fileN");
                Console.WriteLine("   Specifies optional metadata files for each input file. These include additional details about the PSU");
                Console.WriteLine("   like transition states. Files must be separated by a semicolon. If metadata is not present for every file");
                Console.WriteLine("   then multiple semicolons can be specified to omit files whose metadata is not present.");
                Console.WriteLine(" /config=file.xml");
                Console.WriteLine("   Specifies an XMl file that stores all settings that must be used when generating a plot, like its size, and colors used.");
                Console.WriteLine("   If this value is not specified, the default settings will be used.");
                Console.WriteLine(" /genconfig=file.xml");
                Console.WriteLine("   Using this modifier will force the app to write a new XML configuration file to the specified filename, with the defaults");
                Console.WriteLine("   values on all its entries. This file can be used as an starting point for custom config files.");
                Console.WriteLine("   Also note that using this modifier will force the application to quit without reading or plotting any other input file.");
                Console.WriteLine();
                Console.WriteLine(" A " + RUN_FILENAME + " file can be created in the working directory, with commands that are automatically processed");
                Console.WriteLine("  by the tool when executed. These file can include most used settings like /background and /axes.");
                Console.WriteLine(" Arguments on these file must be places one per line, and are appended to the actual command line args.");
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

            Endpoint modeHandler = null;

            ProcessArguments(args);

            if (!string.IsNullOrWhiteSpace(GenConfigFilename))
            {
                EndpointConfig config = new EndpointConfig();
                config.SaveTo(GenConfigFilename);
                Console.WriteLine("Config file written to " + GenConfigFilename);
                return;
            }

            if (LoadRunFile)
            {
                if (File.Exists(RUN_FILENAME))
                {
                    List<string> runargs = new List<string>();
                    using (FileStream fs = new FileStream(RUN_FILENAME, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (TextReader reader = new StreamReader(fs))
                        {
                            string arg = reader.ReadLine();
                            while (arg != null)
                            {
                                arg = arg.Trim();
                                if (arg[0] != '#')
                                    runargs.Add(arg);
                                arg = reader.ReadLine();
                            }
                        }
                    }

                    if (runargs.Count > 0)
                        ProcessArguments(runargs.ToArray());
                }
            }

            if (string.IsNullOrWhiteSpace(ModeName))
            {
                Console.WriteLine("No mode specified. Invoke the command again without parameters to display a list of available modes.");
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            foreach (KeyValuePair<string, Type> endpoint in availableModes)
            {
                if (string.Equals(endpoint.Key, ModeName, StringComparison.OrdinalIgnoreCase))
                {
                    modeHandler = (Endpoint)Activator.CreateInstance(endpoint.Value);
                    break;
                }
            }

            if (modeHandler == null)
            {
                Console.WriteLine("No available mode found with key \"{0}\"", ModeName);
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            // Apply settings
            if (!string.IsNullOrWhiteSpace(ConfigFilename))
            {
                modeHandler.Config = EndpointConfig.Loadfrom(ConfigFilename);

                if (AxisNames != null) modeHandler.Config.AxesNames = AxisNames;
                if (PlotBackgroundColor != null) modeHandler.Config.Background = PlotBackgroundColor.Value;
                if (PlotDpi != null) modeHandler.Config.Dpi = PlotDpi.Value;
            }
            else
            {
                if (AxisNames == null) AxisNames = new string[] {"V", "T"};
                if (PlotBackgroundColor == null) PlotBackgroundColor = Color.Transparent;
                if (PlotDpi == null) PlotDpi = 150;

                modeHandler.Config.AxesNames = AxisNames;
                modeHandler.Config.Background = PlotBackgroundColor.Value;
                modeHandler.Config.Dpi = PlotDpi.Value;
            }

            modeHandler.Initialize();

            for (int i = 0; i < InputFiles.Count; i++)
            {
                string file = InputFiles[i];
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

                Console.WriteLine("Plotting file {0}/{1}  {2}...", (i+1).ToString("N0"), InputFiles.Count.ToString("N0"), fi.Name);

                if (PlotMetadata && i < MetadataFiles.Count && !string.IsNullOrWhiteSpace(MetadataFiles[i]))
                {
                    FileInfo mfi = new FileInfo(MetadataFiles[i]);
                    if (!mfi.Exists)
                    {
                        Console.WriteLine("Warning! Missing metadata file {0} for file {1}.", mfi.Name, fi.Name);
                        modeHandler.Config.MetadataFile = null;
                    }
                    else
                        modeHandler.Config.MetadataFile = mfi.FullName;
                }
                else
                    modeHandler.Config.MetadataFile = null;
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

        private static void ProcessArguments(string[] args)
        {
            foreach (string s in args)
            {
                string p = s.Trim();

                if (p.StartsWith("/") || p.StartsWith("-"))
                {
                    // Switch or modifier
                    if (p.Contains("="))
                    {
                        string[] parts = p.Split(new char[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            Console.WriteLine("Error parsing parameter " + s);
                            Console.WriteLine("Invalid syntax.");
#if DEBUG
                            Console.WriteLine();
                            Console.WriteLine("Press any key to exit");
                            Console.ReadKey();
#endif
                            return;
                        }

                        if (string.Equals(parts[0], "/background", StringComparison.OrdinalIgnoreCase))
                        {
                            if (parts[1].StartsWith("#") && parts[1].Length == 9)
                            {
                                try
                                {
                                    Color c = Color.FromArgb(int.Parse(parts[1].Substring(1, 2)),
                                        int.Parse(parts[1].Substring(3, 2)),
                                        int.Parse(parts[1].Substring(5, 2)), int.Parse(parts[1].Substring(7, 2)));
                                    PlotBackgroundColor = c;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Cannot parse color from code " + parts[1]);
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
                                try
                                {
                                    Color c = Color.FromName(parts[1]);
                                    PlotBackgroundColor = c;
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine("Cannot parse color from name " + parts[1]);
                                    Console.WriteLine(e);
#if DEBUG
                                    Console.WriteLine();
                                    Console.WriteLine("Press any key to exit");
                                    Console.ReadKey();
#endif
                                    return;
                                }
                            }

                            continue;
                        }

                        if (string.Equals(parts[0], "/dpi", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                int value = int.Parse(parts[1]);

                                if (value < 70 || value > 1200)
                                {
                                    Console.WriteLine(
                                        "The specified DPI value is out of bounds. Must be between 70 and 1200.");
#if DEBUG
                                    Console.WriteLine();
                                    Console.WriteLine("Press any key to exit");
                                    Console.ReadKey();
#endif
                                    return;
                                }

                                PlotDpi = value;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot parse dpi setting from string " + parts[1]);
                                Console.WriteLine(e);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }

                            continue;
                        }

                        if (string.Equals(parts[0], "/axes", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                string[] names = parts[1].Split(new char[] { ',' });
                                if (names.Length != 2)
                                {
                                    Console.WriteLine("Invalid axis names.");
#if DEBUG
                                    Console.WriteLine();
                                    Console.WriteLine("Press any key to exit");
                                    Console.ReadKey();
#endif
                                    return;
                                }

                                AxisNames = names;
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot parse axis names from string " + parts[1]);
                                Console.WriteLine(e);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }

                            continue;
                        }

                        if (string.Equals(parts[0], "/metadata", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                string[] names = parts[1].Split(new char[] { ';' });
                                foreach (string name in names)
                                    MetadataFiles.Add(name);
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot parse metadata file names from string " + parts[1]);
                                Console.WriteLine(e);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                        }

                        if (string.Equals(parts[0], "/config", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                if (!File.Exists(parts[1]))
                                    throw new FileNotFoundException("The specified config file cannot be found.");

                                ConfigFilename = parts[1];
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot parse config file from string " + parts[1]);
                                Console.WriteLine(e);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                        }

                        if (string.Equals(parts[0], "/genconfig", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                if (File.Exists(parts[1]))
                                    throw new IOException("The specified config file already exist. Please use another filename.");

                                GenConfigFilename = parts[1];
                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Cannot parse argument from string " + parts[1]);
                                Console.WriteLine(e);
#if DEBUG
                                Console.WriteLine();
                                Console.WriteLine("Press any key to exit");
                                Console.ReadKey();
#endif
                                return;
                            }
                        }
                    }
                    else
                    {
                        if (string.Equals(p, "/nometadata", StringComparison.OrdinalIgnoreCase))
                        {
                            PlotMetadata = false;
                            continue;
                        }

                        if (string.Equals(p, "/norun", StringComparison.OrdinalIgnoreCase))
                        {
                            LoadRunFile = false;
                            continue;
                        }

                        Console.WriteLine("Unknown switch or parameter \"{0}\".", p);
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
                    if (string.IsNullOrWhiteSpace(ModeName))
                        ModeName = s.Trim();
                    else
                        InputFiles.Add(s.Trim());
                }
            }
        }
    }
}
