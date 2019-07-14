using System.Runtime.Serialization;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Generated Rail-specific statistics when the ATX PSU is on the ON state
    /// </summary>
    [DataContract(Name = "OnStageRailStats", Namespace = "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(long))]
    [KnownType(typeof(float))]
    public class OnStageRailStats
    {
        /// <summary>
        /// Stores the percent of time where the ON portion of the rail is In-Regulation
        /// </summary>
        [DataMember] public float InRegulationPercent { get; set; }

        /// <summary>
        /// Stores the mean voltage value for the portion of the rail when its considered to be ON
        /// </summary>
        [DataMember] public float MeanVoltage { get; set; }

        /// <summary>
        /// Stores the deviation from the mean value for the portion of the rail when its considered to be ON
        /// </summary>
        [DataMember] public float DeviationVoltage { get; set; }

        /// <summary>
        /// Stores the percent of time where the ON portion of the rail is Off-Regulation
        /// </summary>
        [DataMember] public float OffRegulationPercent { get; set; }

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