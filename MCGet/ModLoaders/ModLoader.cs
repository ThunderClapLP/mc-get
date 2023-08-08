using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCGet.ModLoaders
{
    public abstract class ModLoader
    {
        public string javaPath = "";
        public abstract bool Install(String minecraftVersion, String loaderVersion);

        public bool DownloadLoader(String url, Spinner spinner) {

            Console.CursorLeft = 0;
            Console.WriteLine("");
            Console.CursorTop -= 1;

            spinner.top = Console.CursorTop;

            Console.Write("Downloading " + Path.GetFileName(url) + " ");

            spinner.Update();

            if (Networking.DownloadFile(url, Program.dir + Program.tempDir + Path.GetFileName(url), spinner))
            {
                ConsoleTools.WriteResult(true);
            }
            else
            {
                //failed
                ConsoleTools.WriteResult(false);
                return false;
            }

            if (Program.DownloadJavaIfNotPresent())
            {
                javaPath = Program.dir + "/java/jdk-19/bin/";
            }

            return true;
        }

        public bool RunLoaderInstaller(Process proc, Spinner spinner, String loaderName) {
            try
            {
                //perform the installation
                proc.Start();

                Console.Write("Installing " + loaderName);
                spinner.top = Console.CursorTop;

                while (!proc.HasExited) {
                    spinner.Update();
                    proc.WaitForExit(100);
                }

                if (proc.ExitCode != 0)
                {
                    //Console.WriteLine(quilt.StandardOutput.ReadToEnd());
                    ConsoleTools.WriteResult(false);
                    Console.WriteLine(proc.StandardError.ReadToEnd());
                    return false;
                }
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
                ConsoleTools.WriteError("Installing " + loaderName + " failed - Is java installed?");
                return false;
            }

            ConsoleTools.WriteResult(true);
            return true;
        }
    }
}
