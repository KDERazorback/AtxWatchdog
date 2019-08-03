using System;
namespace AtxDfuTool
{
    public enum StatusCode : byte
    {
        None = 0xF9,
        Ready = 0xFA,
        Busy = 0xFB,
        WaitingCommand = 0xFC,
        NotImplemented = 0xFD,
        ExecutingSubcommand = 0xFE,
        Terminated = 0xFF,
    }
}
