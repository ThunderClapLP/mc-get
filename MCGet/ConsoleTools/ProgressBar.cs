using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTools
{
    internal class ProgressBar
    {
        public int left;
        public int top;
        public int width;

        public bool alwaysBottom = true;
        public bool fill = false;

        public int value = 0;
        public int max = 100;

        public long lastUpdate = 0;

        public ProgressBar(int left, int top, int width)
        {
            this.left = left;
            this.top = top;
            this.width = width;

            alwaysBottom = false;
        }

        public ProgressBar(int left, int width)
        {
            this.left = left;
            this.width = width;

            alwaysBottom = true;
        }

        public void Update(bool forceDraw = false)
        {
            //if (lastUpdate > Environment.TickCount64 - 50)
            //    return;

            //lastUpdate = Environment.TickCount64;
            if (!CTools.IsConsole)
                return;
            lock (CTools.ConsoleLock)
            {
                bool prvVisible = OperatingSystem.IsWindows() ? Console.CursorVisible : true;
                (int x, int y) prvCurs = Console.GetCursorPosition();

                Console.CursorVisible = false;

                if (fill)
                {
                    left = 0;
                    width = CTools.DockRight();
                }


                if (alwaysBottom)
                {
                    //if (prvTop != top)
                    //{
                    //for (int i = prvLeft; i < width - left; i++)
                    //{
                    //    Console.Write(" ");
                    //}
                    //}
                    if (prvCurs.y >= top)
                    {
                        if (prvCurs.y+ 1 >= Console.BufferHeight)
                        {
                            Console.WriteLine();
                        }
                        else
                        {
                            Console.CursorTop = CTools.DockBottom(); //prvTop + 1;
                            if (prvCurs.y >= CTools.DockBottom())
                            {
                                Console.WriteLine();//Console.CursorTop++;
                            }

                        }
                        top = Console.CursorTop;
                        if (top + 1 >= Console.BufferHeight)
                            prvCurs.y--;
                    }
                    else
                    {
                        Console.CursorTop = CTools.DockBottom(); //prvTop + 1;
                    }

                }

                Console.SetCursorPosition(left, top);

                //Console.ForegroundColor = ConsoleColor.DarkGreen;
                Console.Write("[");
                Console.Write(new String('=', Math.Max((int)(value / (float)max * (width - 5)) - 1, 0)));
                if (width - Console.CursorLeft - 5 > 0)
                {
                    Console.Write(">");
                }
                Console.Write(new String(' ', width - Console.CursorLeft - 5));
                Console.Write("]");
                //Console.ResetColor();

                string percent = ((int)((value / (float)max) * 100)).ToString();
                if (percent.Length == 1)
                    Console.Write("  ");
                else if (percent.Length == 2)
                    Console.Write(" ");

                Console.Write(percent + "%");

                Console.SetCursorPosition(prvCurs.x, prvCurs.y);
                Console.CursorVisible = prvVisible;
            }

        }

        public void Clear()
        {
            if (!CTools.IsConsole)
                return;
                
            lock(CTools.ConsoleLock)
            {
                int prvLeft = Console.CursorLeft;
                int prvTop = Console.CursorTop;

                Console.CursorLeft = left;
                Console.CursorTop = top;
                for (int i = 0; i < width; i++)
                {
                    Console.Write(" ");
                }
                //Console.WriteLine();

                Console.CursorLeft = prvLeft;
                Console.CursorTop = prvTop;
            }
        }
    }
}
