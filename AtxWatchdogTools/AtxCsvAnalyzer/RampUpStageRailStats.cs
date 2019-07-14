using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace AtxCsvAnalyzer
{
    /// <summary>
    /// Generated Rail-specific statistics when the ATX PSU is on the T2 state
    /// </summary>
    [DataContract(Name = "RampUpStageRailStats", Namespace = "com.kderazorback.atxwatchdog")]
    [KnownType(typeof(float))]
    [KnownType(typeof(double))]
    public class RampUpStageRailStats
    {
        /// <summary>
        /// Stores the coefficients used for the Polynomial curve that represents an approximation of the rail behaviour during the T2 state
        /// The first index is the independent var, subsequent values are increasing x-power terms
        /// </summary>
        [DataMember] public double[] CurveCoefficients { get; set; }
        /// <summary>
        /// Stores the values used for the Time Axis (X)
        /// </summary>
        [DataMember] public double[] TimeAxis { get; set; }
        /// <summary>
        /// Stores how well the coefficients fits the original source curve. 1 means a perfect fit
        /// </summary>
        [DataMember] public float Fitness { get; set; }
        /// <summary>
        /// Stores the approximated linear slope for the source curve. This is the a slope value of the quadratic function ax+b
        /// </summary>
        [DataMember] public double Slope { get; set; }
        /// <summary>
        /// Stores the approximated linear Y-intercept value for the source curve. This is the intercept value of the quadratic function ax+b
        /// </summary>
        [DataMember] public double YIntercept { get; set; }


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
