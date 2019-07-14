using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Main container for all generated PSU stats
    /// </summary>
    [DataContract(Name = "AtxStats", Namespace = "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(RailStats))]
    [KnownType(typeof(AtxDeviceMetadata))]
    [KnownType(typeof(long))]
    [KnownType(typeof(float))]
    public class AtxStats
    {
        [DataMember] public RailStats V12Stats { get; set; } = new RailStats() { Rail = Rails.V12, NominalVoltage = 12.0f, VoltageTolerance = 0.05f};
        [DataMember] public RailStats V5Stats { get; set; } = new RailStats() { Rail = Rails.V5, NominalVoltage = 5.0f, VoltageTolerance = 0.05f };
        [DataMember] public RailStats V5SBStats { get; set; } = new RailStats() { Rail = Rails.V5SB, NominalVoltage = 5.0f, VoltageTolerance = 0.05f };
        [DataMember] public RailStats V3_3Stats { get; set; } = new RailStats() { Rail = Rails.V3_3, NominalVoltage = 3.3f, VoltageTolerance = 0.05f };

        [DataMember] public long[][] SourceMetadata { get; set; }

        [DataMember] public AtxDeviceMetadata DeviceInfo { get; set; }

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
