using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Generated Per-Rail Statistics for ATX PSU
    /// </summary>
    [DataContract(Name="RailStats", Namespace= "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(Rails))]
    [KnownType(typeof(RailSegmentStats))]
    [KnownType(typeof(long))]
    [KnownType(typeof(float))]
    public class RailStats
    {
        // Internal field
        private List<RailSegmentStats> _segments = new List<RailSegmentStats>();

        // Properties
        /// <summary>
        /// Stores the rail represented by this data set
        /// </summary>
        [DataMember] public Rails Rail { get; set; }
        /// <summary>
        /// Stores the nominal voltage for this rail
        /// </summary>
        [DataMember] public float NominalVoltage { get; set; }
        /// <summary>
        /// Stores the tolerance deviation specified for this rail, in float format, between 0.0 and 1.0
        /// </summary>
        [DataMember] public float VoltageTolerance { get; set; }
        /// <summary>
        /// Stores the mean voltage value for the entire rail
        /// </summary>
        [DataMember] public float MeanVoltage { get; set; }
        /// <summary>
        /// Stores the max voltage measured on the entire rail
        /// </summary>
        [DataMember] public float MaxVoltage { get; set; }
        /// <summary>
        /// Stores the min voltage measured on the entire rail
        /// </summary>
        [DataMember] public float MinVoltage { get; set; }
        /// <summary>
        /// Stores the RAW data for the Rail
        /// </summary>
        [DataMember] public float[] Points { get; set; }
        /// <summary>
        /// Stores pointers to the <see cref="Points"/> array where there are Peaks or Valleys
        /// </summary>
        [DataMember] public long[] Peaks { get; set; }
        /// <summary>
        /// Stores pointers to the <see cref="Points"/> array where there are Edges
        /// </summary>
        [DataMember] public long[] Edges { get; set; }
        /// <summary>
        /// Stores the sign of the first Peak detected from the rail points. If this value is negative, then the first peak is actually a Valley. Subsequent items are of the opposite type
        /// </summary>
        [DataMember] public int PeakStartingSign { get; set; }


        // The following properties requires metadata to be loaded
        /// <summary>
        /// Stores rail statistics per metadata segments
        /// </summary>
        [DataMember] public RailSegmentStats[] Segments
        {
            get { return _segments.ToArray(); }
            set
            {
                _segments.Clear();
                _segments.AddRange(value);
            }
        }

        /// <summary>
        /// Stores the stats for this rail when the PSU is in the ON stage
        /// </summary>
        [DataMember] public OnStageRailStats OnStageStats { get; set; }

        /// <summary>
        /// Stores the stats for this rail when the PSI is in the T2 ramp-up stage
        /// </summary>
        [DataMember] public RampUpStageRailStats T2StageStats { get; set; }

        // Methods
        public int AppendSegment(RailSegmentStats segment)
        {
            _segments.Add(segment);
            return _segments.Count;
        }

        // Serialization methods
        public void SerializeTo(string filename)
        {
            SerializationHelper.SerializeTo(filename, this);
        }

        public byte[] SerializeToArray()
        {
            return SerializationHelper.SerializeToArray(this);
        }
    }
}
