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
        public static void WriteResult(bool success)
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

        public static void WriteError(string err, int level = 2)
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

        public static bool ConfirmDialog(string prompt, bool defaultRes = false)
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
            if (Console.WindowTop + Console.WindowHeight < Console.BufferHeight)
            {
                return Console.WindowTop + Console.WindowHeight - 1;
            }
            else
            {
                return Console.BufferHeight - 1;
            }
        }

        public static void ClearLine(bool fromStart = true)
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
