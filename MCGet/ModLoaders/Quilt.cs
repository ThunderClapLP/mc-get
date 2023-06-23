using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MCGet.ModLoaders
{
    public class Quilt : ModLoader
    {
        static string url = "https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-installer/0.5.1/quilt-installer-0.5.1.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                //no version given
                ConsoleTools.WriteError("Could not install Quilt");
                return false;
            }

            loaderVersion = loaderVersion.Replace("quilt-", ""); //make sure version has the correct format

            Console.CursorLeft = 0;
            Console.WriteLine("");
            Console.CursorTop -= 1;

            Spinner spinner = new Spinner(Console.CursorTop);
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


            string javaPath = "";
            if (Program.DownloadJavaIfNotPresent())
            {
                javaPath = Program.dir + "/java/jdk-19/bin/";
            }

            try
            {
                //perform the installation
                Process quilt = new Process();
                quilt.StartInfo.FileName = javaPath + "java";
                quilt.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" install client " + minecraftVersion + " " + loaderVersion + " --install-dir=\"" + Program.minecraftDir + "\"";
                quilt.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

                quilt.StartInfo.RedirectStandardOutput = true;
                quilt.StartInfo.RedirectStandardError = true;
                quilt.StartInfo.RedirectStandardInput = true;
                quilt.Start();

                Console.Write("Installing Quilt");
                spinner.top = Console.CursorTop;

                while (!quilt.HasExited) {
                    spinner.Update();
                    quilt.WaitForExit(100);
                }

                if (quilt.ExitCode != 0)
                {
                    //Console.WriteLine(quilt.StandardOutput.ReadToEnd());
                    ConsoleTools.WriteResult(false);
                    Console.WriteLine(quilt.StandardError.ReadToEnd());
                    return false;
                }
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
                ConsoleTools.WriteError("Installing Quilt failed - Is java installed?");
                return false;
            }

            ConsoleTools.WriteResult(true);
            return true;
        }
    }
}
