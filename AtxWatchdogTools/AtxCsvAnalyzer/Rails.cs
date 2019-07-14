using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Enumerates all possible rails that can be diagnoses on an ATX PSU
    /// </summary>
    public enum Rails
    {
        V12,
        V5,
        V5SB,
        V3_3,
        V12N,
        V5N
    }
}
