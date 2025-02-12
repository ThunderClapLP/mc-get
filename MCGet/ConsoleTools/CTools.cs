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
        public static bool IsConsole = true;
        public static bool SupportsColor = true;

        public static void ValidateConsole()
        {
            try { IsConsole = Console.WindowHeight > 0; }
            catch { IsConsole = false; }
            try { var col = Console.BackgroundColor; Console.BackgroundColor = col; SupportsColor = true; }
            catch { SupportsColor = false; }
        }

        public static int CursorTop
        {
            get {
                if (!IsConsole)
                    return 0;
                int val = 0;
                lock(ConsoleLock) val = Console.CursorTop;
                return val;
                }
            set {
                if (!IsConsole)
                    return;
                lock (ConsoleLock) Console.CursorTop = value;
            }
        }
        public static int CursorLeft
        {
            get {
                if (!IsConsole)
                    return 0;
                int val = 0;
                lock(ConsoleLock) val = Console.CursorLeft;
                return val;
            }
            set {
                if (!IsConsole)
                    return;
                lock (ConsoleLock) Console.CursorLeft = value;
            }
        }

        public static void SetCursorPosition(int left, int top)
        {
            if (!IsConsole)
                return;
            lock (ConsoleLock) Console.SetCursorPosition(left, top);
        }

        public static (int Left, int Top) GetCursorPosition()
        {
            (int, int) val = (0, 0);
            if (IsConsole)
                lock (ConsoleLock) val = Console.GetCursorPosition();
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
        //TODO: add optional spinner attribute to draw the result at spinner pos and write it's msg if not in terminal mode
        public static void WriteResult(bool success, Spinner? spinner = null)
        {
            lock(ConsoleLock)
            {
                if (success)
                {
                    if (IsConsole)
                        Console.CursorLeft = DockRight() - 4;
                    Console.Write("  ");
                    if (SupportsColor)
                        Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK");
                }
                else
                {
                    if (IsConsole)
                        Console.CursorLeft = DockRight() - 8;
                    Console.Write("  ");
                    if (SupportsColor)
                        Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("FAILED");
                }

                //Console.BackgroundColor=col;
                if (SupportsColor)
                    Console.ResetColor();
            }
        }

        public static void WriteError(string err, int level = 2)
        {
            lock(ConsoleLock)
            {
                if (level == 2)
                {
                    if (SupportsColor)
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                    Console.Write("ERROR: ");
                }
                else if (level == 1)
                {
                    //Console.BackgroundColor = ConsoleColor.DarkYellow;
                    if (SupportsColor)
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("WARN: ");
                } else
                {
                    if (SupportsColor)
                        Console.ForegroundColor = ConsoleColor.Blue;
                    Console.Write("INFO: ");
                }
                //Console.BackgroundColor = col;
                if (SupportsColor)
                    Console.ResetColor();

                Console.WriteLine(err);
            }
        }

        public static char ChoiceDialog(string prompt, char[] choices, char defaultRes)
        {
            lock (ConsoleLock)
            {
                bool exit = false;
                while (!exit)
                {
                    Console.Write(prompt + " [" + string.Join("/", choices).ToLower().Replace(defaultRes.ToString(), defaultRes.ToString().ToUpper()) + "]: ");

                    if (SilentMode)
                    {
                        Console.WriteLine(defaultRes.ToString());
                        return defaultRes;
                    }

                    ConsoleKeyInfo key = new ConsoleKeyInfo(defaultRes, ConsoleKey.Enter, false, false, false);
                    try {key = Console.ReadKey(true); }
                    catch {}
                    if (key.Key == ConsoleKey.Enter)
                    {
                        Console.Write(defaultRes.ToString());
                        exit = true;
                    }
                    else if (choices.Contains(key.KeyChar))
                    {
                        exit = true;
                        defaultRes = key.KeyChar;
                    }
                    else Console.Write("Illegal Input");
                    Console.WriteLine();
                }
            }
            return defaultRes;
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
                        Console.WriteLine(defaultRes ? "y" : "n");
                        return defaultRes;
                    }

                    ConsoleKey key = ConsoleKey.Enter;
                    try {key = Console.ReadKey(true).Key; }
                    catch {}
                    switch (key)
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
            if (!IsConsole)
                return 0;
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
            if (!IsConsole)
                return 0;
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
            lock (ConsoleLock)
            {
                if (!IsConsole)
                {
                    Console.WriteLine();
                    return;
                }
                if (fromStart)
                {
                    Console.CursorLeft = 0;
                }
                (int x, int y) prevCurs = Console.GetCursorPosition();
                
                Console.WriteLine(new String(' ', DockRight() - prevCurs.x)); //writeline to have the same behaviour on all console widths
                
                Console.SetCursorPosition(prevCurs.x, Console.GetCursorPosition().Top - 1); //don't use prevCury.y to fix an extra line being spawned on linux
            }
        }
    }
}
