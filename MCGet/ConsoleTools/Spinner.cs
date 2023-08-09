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

        public bool dockRight = true;

        private int state = 0;
        private char curr = '/';

        public long lastUpdate = 0;
        public int minSpinnerTime = 200;

        public Spinner(int left, int top)
        {
            this.left = left;
            this.top = top;

            dockRight = false;
        }

        public Spinner(int top)
        {
            this.left = CTools.DockRight();
            this.top = top;

            dockRight = true;
        }

        public override void Update()
        {
            if (lastUpdate > Environment.TickCount64 - minSpinnerTime)
                return;
            lastUpdate = Environment.TickCount64;

            switch (state)
            {
                case 0:
                    curr = '-';
                    break;
                case 1:
                    curr = '\\';
                    break;
                case 2:
                    curr = '|';
                    break;
                case 3:
                    curr = '/';
                    state = -1;
                    break;
                default:
                    state = -1;
                    break;
            }
            state++;

            Draw();
        }

        public void Draw()
        {
            lock(CTools.ConsoleLock)
            {
                bool prvVisible = OperatingSystem.IsWindows() ? Console.CursorVisible : true;
                int prvLeft = Console.CursorLeft;
                int prvTop = Console.CursorTop;

                Console.CursorVisible = false;
                Console.CursorLeft = dockRight ? CTools.DockRight() - 1 : left;
                Console.CursorTop = top;


                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write(curr);
                Console.ResetColor();

                Console.CursorLeft = prvLeft;
                Console.CursorTop = prvTop;
                Console.CursorVisible = prvVisible;
            }

        }

        public void Clean()
        {
            char prv = curr;
            curr = ' ';
            Draw();
            curr = prv;
        }
    }
}
