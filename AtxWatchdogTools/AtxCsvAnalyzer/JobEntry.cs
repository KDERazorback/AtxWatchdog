using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    internal class JobEntry
    {
        public string InputFilename { get; set; }
        public string MetadataFilename { get; set; }
        public string InfoFilename { get; set; }
        public bool GenerateTar { get; set; } = false;
        public bool GenerateCsv { get; set; } = true;
        public string OutputFilename { get; set; }
    }
}
