using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTools
{
    public class Spinner : AnimatableTool
    {
        public int left;
        public int top;

        private string _msg = "";
        public string msg
        {
            get { return _msg; }
            set
            {
                if (CTools.IsConsole || msg != value)
                    msgPrinted = false;
                _msg = value;
            }
        }
        private bool msgPrinted = false;

        public bool dockRight = true;

        public string animChars = "-\\|/"; //"⣷⣯⣟⡿⢿⣻⣽⣾"; //"◐◓◑◒";
        private int state = 0;

        public long lastUpdate = 0;
        public int minSpinnerTime = CTools.IsConsole ? 200 : 1000;

        public Spinner(int left, int top)
        {
            this.left = left;
            this.top = top;

            dockRight = false;
        }

        public Spinner(int top)
        {
            this.left = CTools.IsConsole ? CTools.DockRight() : 80;
            this.top = top;

            dockRight = true;
        }

        public Spinner(string msg, int left, int top)
        {
            this.msg = msg;

            this.left = left;
            this.top = top;

            dockRight = true;
        }

        public Spinner(string msg, int top)
        {
            this.msg = msg;

            this.left = CTools.IsConsole ? CTools.DockRight() : 80;
            this.top = top;

            dockRight = true;
        }

        private void PrintMsg()
        {
            CTools.CursorLeft = 0;
            Console.Write(CTools.LimitText(msg, Math.Max(left - 1, 0), true));
            CTools.CursorLeft = Math.Min(left - 1, msg.Length);
        }

        public override void Update(bool forceDraw = false)
        {
            if (lastUpdate > Environment.TickCount64 - minSpinnerTime)
            {
                if (forceDraw)
                    Draw();
                return;
            }
            lastUpdate = Environment.TickCount64;

            state = (state + 1) % animChars.Length;

            Draw();
        }

        public void Draw()
        {
            lock (CTools.ConsoleLock)
            {
                if (!CTools.IsConsole && msg == "")
                {
                    Console.Write(".");
                    return;
                }

                if (!CTools.IsConsole || !msgPrinted)
                {
                    if (msg != "")
                        PrintMsg();
                    msgPrinted = true;
                }
                bool prvVisible = true;
                int prvLeft = 0;
                int prvTop = 0;

                if (CTools.IsConsole)
                {
                    prvVisible = OperatingSystem.IsWindows() ? Console.CursorVisible : true;
                    prvLeft = Console.CursorLeft;
                    prvTop = Console.CursorTop;

                    Console.CursorVisible = false;
                    Console.CursorLeft = dockRight ? CTools.DockRight() - 1 : left;
                    Console.CursorTop = top;
                }

                if (CTools.SupportsColor)
                    Console.ForegroundColor = ConsoleColor.Blue;
                if (state >= 0 && state < animChars.Length)
                {
                    Console.Write(animChars[state]);
                }
                else
                {
                    Console.Write(' ');
                }
                if (CTools.SupportsColor)
                    Console.ResetColor();

                if (CTools.IsConsole)
                {
                    Console.CursorLeft = prvLeft;
                    Console.CursorTop = prvTop;
                    Console.CursorVisible = prvVisible;
                }
                else
                    Console.WriteLine();
            }

        }

        public void Clean()
        {
            int prvState = state;
            state = -1;
            Draw();
            state = prvState;
        }
    }
}
