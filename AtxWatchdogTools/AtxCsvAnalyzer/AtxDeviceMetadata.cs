using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Container for metadata details about the physical ATX device
    /// </summary>
    [DataContract(Name = "AtxDeviceMetadata", Namespace = "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(string))]
    [KnownType(typeof(int))]
    [KnownType(typeof(AtxPhysicalFormFactor))]
    public class AtxDeviceMetadata
    {
        [DataMember] public string Brand { get; set; }
        [DataMember] public string Model { get; set; }
        [DataMember] public string SerialNumber { get; set; }
        [DataMember] public int Wattage { get; set; }
        [DataMember] public int ManufactureYear { get; set; }
        [DataMember] public AtxPhysicalFormFactor FormFactor { get; set; }
        [DataMember] public bool IsGood { get; set; }
        [DataMember] public string Tag { get; set; }

        // Automatically generated
        [DataMember] public DateTime TestDate { get; set; } = DateTime.Now;

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
