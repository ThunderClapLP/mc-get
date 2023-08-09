using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTools
{
    public class CTools
    {
        public static int MaxWidth = 100;
        public static bool SilentMode = false;
        public static object ConsoleLock = new object();

        public static int CursorTop
        {
            get {
                    int val = 0;
                    lock(ConsoleLock) val = Console.CursorTop;
                    return val;
                }
            set { lock (ConsoleLock) Console.CursorTop = value; }
        }
        public static int CursorLeft
        {
            get { 
                int val = 0;
                lock(ConsoleLock) val = Console.CursorLeft;
                return val;
            }
            set { lock (ConsoleLock) Console.CursorLeft = value; }
        }

        public static void SetCursorPosition(int left, int top)
        {
            lock(ConsoleLock) Console.SetCursorPosition(left, top);
        }

        public static (int Left, int Top) GetCursorPosition()
        {
            (int, int) val;
            lock(ConsoleLock) val = Console.GetCursorPosition();
            return val;
        }

        public static void WriteLine(dynamic value)
        {
            value ??= "";
            lock(ConsoleLock) Console.WriteLine(value);
        }

        public static void WriteLine(string format, params object[] list)
        {
            lock(ConsoleLock) Console.WriteLine(format, list);
        }

        public static void WriteLine() {
            WriteLine("");
        }

        public static void Write(dynamic value)
        {
            value ??= "";
            lock(ConsoleLock) Console.Write(value);
        }

        public static void Write(string format, params object[] list)
        {
            lock(ConsoleLock) Console.Write(format, list);
        }
        public static void WriteResult(bool success)
        {
            lock(ConsoleLock)
            {
                ConsoleColor col = Console.BackgroundColor;

                if (success)
                {
                    Console.CursorLeft = DockRight() - 4;
                    Console.Write("  ");
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK");
                }
                else
                {
                    Console.CursorLeft = DockRight() - 8;
                    Console.Write("  ");
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                }

                //Console.BackgroundColor=col;
                Console.ResetColor();
            }
        }

        public static void WriteError(string err, int level = 2)
        {
            lock(ConsoleLock)
            {
                ConsoleColor col = Console.BackgroundColor;

                if (level == 2)
                {
                    Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.Write("ERROR: ");
                }
                else if (level == 1)
                {
                    //Console.BackgroundColor = ConsoleColor.DarkYellow;
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("WARN: ");
                } else
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("INFO: ");
                }
                //Console.BackgroundColor = col;
                Console.ResetColor();

                Console.WriteLine(err);
            }
        }

        public static bool ConfirmDialog(string prompt, bool defaultRes = false)
        {
            lock(ConsoleLock)
            {
                bool exit = false;
                while (!exit)
                {
                    Console.Write(prompt + " [" + (defaultRes?"Y":"y") + "/" + (defaultRes?"n":"N") + "]: ");

                    if (SilentMode)
                    {
                        Console.WriteLine();
                        return defaultRes;
                    }

                    ConsoleKeyInfo key = Console.ReadKey(true);
                    switch (key.Key)
                    {
                        case ConsoleKey.Y:
                            exit = true;
                            defaultRes = true;
                            Console.Write("y");
                            break;
                        case ConsoleKey.N:
                            exit = true;
                            defaultRes = false;
                            Console.Write("n");
                            break;
                        case ConsoleKey.Enter:
                            exit = true;
                            Console.Write(defaultRes ? "y" : "n");
                            break;
                        default:
                            Console.Write("Illegal Input");
                            break;
                    }
                    Console.WriteLine();
                }
            }
            return defaultRes;
        }

        public static int DockRight()
        {
            if (Console.WindowWidth <= Console.BufferWidth && MaxWidth > Console.WindowWidth)
            {
                return Console.WindowWidth;
            }
            else if (MaxWidth < Console.BufferWidth)
            {
                return MaxWidth;
            }
            else
            {
                return Console.BufferWidth;
            }
        }

        public static int DockBottom()
        {
            int val;
            if (Console.WindowTop + Console.WindowHeight < Console.BufferHeight)
            {
                val =  Console.WindowTop + Console.WindowHeight - 1;
            }
            else
            {
                val = Console.BufferHeight - 1;
            }

            return val;
        }

        public static void ClearLine(bool fromStart = true)
        {
            lock(ConsoleLock)
            {
                if (fromStart)
                {
                    Console.CursorLeft = 0;
                }
                (int x, int y) prevCurs = Console.GetCursorPosition();
                

                for (int i = Console.CursorLeft; i < DockRight() - 1; i++) //-1 needed for linux. buggy else
                {
                    Console.Write(" ");
                }
                Console.SetCursorPosition(prevCurs.x, prevCurs.y);
            }
        }
    }
}
