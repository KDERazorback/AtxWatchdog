using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    class Program
    {
#if DEBUG
        static void Main(string[] args)
        {
            Main_wrap(args);
            Console.WriteLine();
            Console.WriteLine("-- Press any key to exit --");
            Console.ReadKey();
        }
        static void Main_wrap(string[] args)
#else
        static void Main(string[] args)
#endif
        {
            if (args == null || args.Length < 2)
            {
                Console.WriteLine("AtxCsvAnalyzer");
                Console.WriteLine("Copyright (c) Fabian Ramos 2019");
                Console.WriteLine();
                Console.WriteLine("Analyzes and generates PSU statistics from CSV files coming from watchdog boards");
                Console.WriteLine("Usage:");
                Console.WriteLine("       atxcsvanalyze.exe [/m=file1metadata.bin] [/tar|/csv|/full] [/tardir=<path>] [/x=file1info.txt|/x=@] <file1.csv> <outfile1.csv> ... [/m=fileNmetadata.bin] [fileN.csv] [outfileN.csv]");
                Console.WriteLine();
                Console.WriteLine("  Analyzes multiple input files and store the results on the specified output file as CSV.");
                Console.WriteLine("  If the specified output file exists, it will be overwritten.");
                Console.WriteLine("  NOTE: The output filename's extension is not guaranteed to be used.");
                Console.WriteLine("        CSV stat files are written in CSV format regardless of the extension specified.");
                Console.WriteLine("        DUMP files are written in XML format regardless of the extension specified.");
                Console.WriteLine("        TAR archives are written with a .tar.gz exension.");
                Console.WriteLine();
                Console.WriteLine("     All options below are valid for the next input file only.");
                Console.WriteLine();
                Console.WriteLine("  /m=file.bin");
                Console.WriteLine("     Loads the specified metadata binary file for further data analysis.");
                Console.WriteLine("     Use /m to set the metadata for the next input file.");
                Console.WriteLine("     If no metadata is specified, only partial statistics will be generated.");
                Console.WriteLine("  /x=infofile.txt|@");
                Console.WriteLine("     Loads the specified info file (in plain text format) with details about the physica hardware specs.");
                Console.WriteLine("     for the input datastream. This only serves the purpose of decorating output data.");
                Console.WriteLine("     If you specifify '@' as the value, this data will be prompted at runtime.");
                Console.WriteLine("     If this parameter is missing entirely, then no info will be appended to the output stats.");
                Console.WriteLine("  /tar");
                Console.WriteLine("     Generates an output compressed tar.gz archive only with stats files.");
                Console.WriteLine("     This option is per input file.");
                Console.WriteLine("  /tardir=path");
                Console.WriteLine("     Specifies an additional directory to include on the output Tar archive.");
                Console.WriteLine("     This option has effect only with /tar.");
                Console.WriteLine("     This option can be specified multiple times to include multiple directories.");
                Console.WriteLine("     This option is per input file.");
                Console.WriteLine("  /csv");
                Console.WriteLine("     Generates multiple CSV output stats files.");
                Console.WriteLine("     This option is per input file.");
                Console.WriteLine("  /full");
                Console.WriteLine("     Generates both multiple CSV and a compressed .tar.gz files.");
                Console.WriteLine("     This option is per input file.");

#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            Queue<JobEntry> inputFiles = new Queue<JobEntry>();
            JobEntry tmpEntry = new JobEntry();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("/m=", StringComparison.OrdinalIgnoreCase))
                {
                    string file = args[i].Substring(3);
                    FileInfo fi = new FileInfo(file);
                    if (!fi.Exists)
                    {
                        Console.WriteLine("Error!: Specified metadata file " + file + " cannot be found.");
                        return;
                    }

                    tmpEntry.MetadataFilename = fi.FullName;

                    Console.WriteLine("Setting metadata file for next input file: " + file);
                    continue;
                }

                if (args[i].StartsWith("/x=", StringComparison.OrdinalIgnoreCase) && !string.Equals(args[i], "/x=@", StringComparison.OrdinalIgnoreCase))
                {
                    string file = args[i].Substring(3);
                    FileInfo fi = new FileInfo(file);
                    if (!fi.Exists)
                    {
                        Console.WriteLine("Error!: Specified info file " + file + " cannot be found.");
                        return;
                    }

                    tmpEntry.InfoFilename = fi.FullName;

                    Console.WriteLine("Setting info file for next input file: " + file);
                    continue;
                }

                if (args[i].StartsWith("/tardir=", StringComparison.OrdinalIgnoreCase))
                {
                    string dir = args[i].Substring(8);
                    DirectoryInfo di = new DirectoryInfo(dir);
                    if (!di.Exists)
                    {
                        Console.WriteLine("Error!: Specified extra directory " + di + " cannot be found.");
                        return;
                    }

                    tmpEntry.ExtraDirs.Add(dir, di.FullName);

                    Console.WriteLine("+ Adding extra data directory: " + di);
                    continue;
                }

                if (string.Equals(args[i], "/x=@", StringComparison.OrdinalIgnoreCase))
                {
                    tmpEntry.InfoFilename = "@";
                    continue;
                }

                if (string.Equals(args[i], "/tar", StringComparison.OrdinalIgnoreCase))
                {
                    tmpEntry.GenerateTar = true;
                    tmpEntry.GenerateCsv = false;
                    continue;
                }

                if (string.Equals(args[i], "/csv", StringComparison.OrdinalIgnoreCase))
                {
                    tmpEntry.GenerateTar = false;
                    tmpEntry.GenerateCsv = true;
                    continue;
                }

                if (string.Equals(args[i], "/full", StringComparison.OrdinalIgnoreCase))
                {
                    tmpEntry.GenerateTar = true;
                    tmpEntry.GenerateCsv = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(tmpEntry.InputFilename))
                {
                    FileInfo fi2 = new FileInfo(args[i]);
                    if (!fi2.Exists)
                    {
                        Console.WriteLine("Error!: The specified input data file doesnt exists. " + args[i]);
                        return;
                    }
                    tmpEntry.InputFilename = fi2.FullName;
                    continue;
                }

                FileInfo fo = new FileInfo(args[i]);
                if (!fo.Directory?.Exists ?? false)
                    fo.Directory.Create();

                tmpEntry.OutputFilename = fo.FullName;

                inputFiles.Enqueue(tmpEntry);
                tmpEntry = new JobEntry();
            }

            if (inputFiles.Count < 1)
            {
                Console.WriteLine("No input files to process. Aborted.");
                return;
            }

            int fileIndex = 0;
            int totalFiles = inputFiles.Count;
            while (inputFiles.Count > 0)
            {
                JobEntry job = inputFiles.Dequeue();

                Console.WriteLine("Analyzing input file " + (fileIndex + 1).ToString() + "/" + totalFiles.ToString());

                try
                {
                    float[][] matrix = Matrix.LoadMatrix(job.InputFilename, true, out string[] headers);

                    Dictionary<Rails, float[]> series = new Dictionary<Rails, float[]>();
                    float[] timeSeries = null;
                    for (int i = 0; i < headers.Length; i++)
                    {
                        Rails rail;
                        if (Rails.TryParse(headers[i], true, out rail))
                            series.Add(rail, matrix[i]);

                        if (headers[i] == "t0")
                            timeSeries = matrix[i];
                    }

                    AtxStaticAnalyzer analyzer = new AtxStaticAnalyzer();
                    analyzer.SetRailsFromDictionary(series);
                    analyzer.TimeSerie = timeSeries;

                    if (!string.IsNullOrWhiteSpace(job.MetadataFilename))
                        analyzer.LoadMetadata(job.MetadataFilename);

                    AtxStats stats = analyzer.Run();

                    if (!string.IsNullOrWhiteSpace(job.InfoFilename))
                    {
                        AtxDeviceMetadata metadata = new AtxDeviceMetadata();
                        if (job.InfoFilename == "@")
                        {
                            object objRef = (object) metadata;
                            ConsoleExtensions.EditClass(ref objRef, "Specify ATX physical device properties below:");

                            Console.Clear();
                            Console.WriteLine("Device info has been written.");
                            metadata = (AtxDeviceMetadata) objRef;
                        }
                        else
                            metadata = SerializationHelper.DeserializeFrom<AtxDeviceMetadata>(job.InfoFilename);

                        stats.DeviceInfo = metadata;
                    }

                    AtxStatsDumper dumper = new AtxStatsDumper(job.OutputFilename);
                    dumper.ExtraData = analyzer.LogStream;
                    dumper.ExtraDirectories = job.ExtraDirs.ToArray();
                    dumper.ExtraDataName = "last_log.txt";
                    if (job.GenerateCsv)
                        dumper.DumpCsvs(stats);
                    if (job.GenerateTar)
                        dumper.DumpGzipped(stats);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An internal error occurred while processing the specified input file.");
                    Console.WriteLine(e);
                }

                fileIndex++;
            }

            Console.WriteLine("{0} files processed.", totalFiles.ToString());
        }
    }
}
