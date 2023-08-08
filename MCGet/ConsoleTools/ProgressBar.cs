﻿using System;
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

        public void Update()
        {
            //if (lastUpdate > Environment.TickCount64 - 50)
            //    return;
            
            //lastUpdate = Environment.TickCount64;

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
            for (int i = 1; i < width - 5; i++)
            {
                if ((i / (float)(width - 5)) < (value / (float)max))
                {
                    //Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("=");
                }
                else if (((i - 1) / (float)(width - 5)) < (value / (float)max))
                {
                    //Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write(">");
                } else
                {
                    Console.Write(' ');
                }
            }
            //Console.ForegroundColor = ConsoleColor.DarkGreen;
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

        public void Clear()
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