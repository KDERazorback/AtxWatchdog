using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvDataConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  atxcsv.exe <binFile1> ... [binFileN]");
                Console.WriteLine(" This tool will convert all specified files to CSV sheets. Output files will be named according to input files");
#if DEBUG
                Console.WriteLine();
                Console.WriteLine("Press any key to exit");
                Console.ReadKey();
#endif
                return;
            }

            foreach (string file in args)
            {
                if (string.IsNullOrWhiteSpace(file))
                    continue;

                try
                {
                    FileInfo fi = new FileInfo(file);
                    string outputFilename = fi.FullName.Substring(0, fi.FullName.Length - fi.Extension.Length) + ".csv";
                    using (FileStream fin = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (FileStream fout = new FileStream(outputFilename, FileMode.Create, FileAccess.Write,
                            FileShare.None))
                        {
                            StringBuilder str = new StringBuilder();
                            str.AppendLine("v12,v5,v5sb,v3_3");
                            byte[] foutBuffer = Encoding.ASCII.GetBytes(str.ToString());
                            fout.Write(foutBuffer, 0, foutBuffer.Length);
                            str.Length = 0;

                            byte[] buffer = new byte[8];
                            while (fin.Position < fi.Length)
                            {
                                int read = fin.Read(buffer, 0, buffer.Length);
                                if (read < buffer.Length)
                                {
                                    Console.WriteLine("Warning: The data stream is not aligned properly.");
                                    break;
                                }

                                for (int i = 0; i < buffer.Length; i += 2)
                                {
                                    UInt16 value;
                                    value = buffer[i];
                                    value <<= 8;
                                    value += buffer[i + 1];
                                    double fvalue = value > 0 ? value / 1000.0d : 0;

                                    str.Append(fvalue.ToString("N3"));

                                    if (i + 2 < buffer.Length - 1)
                                        str.Append(",");
                                    else
                                        str.AppendLine();
                                }
                                foutBuffer = Encoding.ASCII.GetBytes(str.ToString());
                                fout.Write(foutBuffer, 0, foutBuffer.Length);
                                str.Length = 0;
                            }
                        }
                    }
                    Console.WriteLine("File {0} processed successfully.", fi.Name);
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred while processing input file \"" + file + "\".");
                    Console.WriteLine(e);
                }
            }

            Console.WriteLine("All files processed successfully.");
#if DEBUG
            Console.WriteLine();
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
#endif
        }
    }
}
