using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Stores statistics for an specific ATX Rail and timelapse
    /// </summary>
    [DataContract(Name="RailSegmentStats", Namespace= "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(long))]
    [KnownType(typeof(float))]
    public class RailSegmentStats
    {
        /// <summary>
        /// Stores a value indicating if the segment cannot be processed due to missing metadata
        /// </summary>
        [DataMember] public bool MetadataIncomplete { get; set; } = false;
        /// <summary>
        /// Stores the name of the signal where this segment starts
        /// </summary>
        [DataMember] public string FromSignal { get; set; }
        /// <summary>
        /// Stores the name of the signal where this segment ends
        /// </summary>
        [DataMember] public string ToSignal { get; set; }
        /// <summary>
        /// Stores the mean voltage value for the entire rail segment
        /// </summary>
        [DataMember] public float MeanVoltage { get; set; }
        /// <summary>
        /// Stores the deviation of the rail from the mean value
        /// </summary>
        [DataMember] public float Deviation { get; set; }
        /// <summary>
        /// Stores the max voltage measured on the entire rail segment
        /// </summary>
        [DataMember] public float MaxVoltage { get; set; }
        /// <summary>
        /// Stores the min voltage measured on the entire rail segment
        /// </summary>
        [DataMember] public float MinVoltage { get; set; }
        /// <summary>
        /// Stores the data points that forms the current segment
        /// </summary>
        [DataMember] public float[] Points { get; set; }
        /// <summary>
        /// Stores the duration of the segment in frames
        /// </summary>
        [DataMember] public long DurationFrames { get; set; }
        /// <summary>
        /// Stores the duration of the segment in microseconds (us)
        /// </summary>
        [DataMember] public long DurationUs { get; set; }

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
