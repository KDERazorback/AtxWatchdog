using System;
namespace AtxDfuTool
{
    public enum DfuProgramCodes
    {
        None = 0,
        Debug = 1,
        BandgapCalibration = 5,
        V12Calibration = 6,
        V5Calibration = 7,
        V5SBCalibration = 8,
        V3_3Calibration = 9,
        ARefCalibration = 10,
    }
}
