using System;
using System.Text;

namespace AtxDataDumper
{
    public class ConsoleOverlay
    {
        public float PositionX { get; set; } = 1.0f;

        protected string Spinner = "/-\\|";
        protected int SpinnerIndex = 0;

        protected string Message;
        protected bool IsVisible;
        protected bool EnableSpinner = true;


        public void ShowOverlay()
        {
            if (string.IsNullOrWhiteSpace(Message)) return;

            if (Console.CursorLeft > 0) Console.WriteLine();

            int startPos = Console.WindowWidth - Message.Length - (EnableSpinner ? 2 : 1);
            if (startPos < 0) startPos = 0;
            if (Message.Length >= Console.WindowWidth - (EnableSpinner ? 2 : 1)) Message = Message.Substring(0, Console.WindowWidth - (EnableSpinner ? 2 : 1));

            if (EnableSpinner) Message += Spinner[SpinnerIndex];

            ConsoleColor back = Console.BackgroundColor;
            ConsoleColor front = Console.ForegroundColor;

            Console.BackgroundColor = ConsoleColor.Gray;
            Console.ForegroundColor = ConsoleColor.Black;

            Console.CursorLeft = startPos;
            Console.Write(Message);
            Console.CursorLeft = 0;

            Console.BackgroundColor = back;
            Console.ForegroundColor = front;

            IsVisible = true;
        }

        public void ShowOverlay(string msg)
        {
            if (IsVisible) ClearOverlay();
            SpinnerIndex += 1;
            if (SpinnerIndex >= Spinner.Length) SpinnerIndex = 0;
            Message = msg;
            ShowOverlay();
        }

        public void ClearOverlay()
        {
            if (!IsVisible) return;

            int startPos = Console.WindowWidth - Message.Length - 1;
            if (startPos < 0) startPos = 0;

            StringBuilder str = new StringBuilder(Message.Length);
            while (str.Length < Message.Length)
                str.Append(' ');

            Console.CursorLeft = startPos;
            Console.Write(str.ToString());
            Console.CursorLeft = 0;

            IsVisible = false;
        }
    }
}
