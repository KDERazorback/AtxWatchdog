using System;
namespace AtxDataDumper
{
    public class RunningAvg
    {
        public ulong Count { get; protected set; } = 0;
        public float Mean { get; protected set; } = 0;
        public int Factor { get; set; } = 1000;

        public void Add(float value)
        {
            Mean = Mean + ((value - Mean) / Math.Min((float)Count + 1, (float)Factor));
            Count++;
        }

        public void Reset()
        {
            Count = 0;
            Mean = 0;
        }
    }
}
