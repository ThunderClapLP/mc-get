using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCGet.ModLoaders
{
    public class Fabric : ModLoader
    {
        static string url = "https://maven.fabricmc.net/net/fabricmc/fabric-installer/0.11.1/fabric-installer-0.11.1.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                //no version given
                ConsoleTools.WriteError("Could not install fabric");
                return false;
            }

            loaderVersion = loaderVersion.Replace("fabric-", ""); //make sure version has the correct format

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
                Process fabric = new Process();
                fabric.StartInfo.FileName = javaPath + "java";
                fabric.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" client -mcversion " + minecraftVersion + " -loader" + loaderVersion + " -dir \"" + Program.minecraftDir + "\"";
                fabric.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

                fabric.StartInfo.RedirectStandardOutput = true;
                fabric.StartInfo.RedirectStandardError = true;
                fabric.StartInfo.RedirectStandardInput = true;
                fabric.Start();

                Console.Write("Installing Fabric");
                spinner.top = Console.CursorTop;

                while (!fabric.HasExited) {
                    spinner.Update();
                    Thread.Sleep(100);
                }

                fabric.WaitForExit();

                if (fabric.ExitCode != 0)
                {
                    //Console.WriteLine(quilt.StandardOutput.ReadToEnd());
                    ConsoleTools.WriteResult(false);
                    Console.WriteLine(fabric.StandardError.ReadToEnd());
                    return false;
                }
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
                ConsoleTools.WriteError("Installing Fabric failed - Is java installed?");
                return false;
            }

            ConsoleTools.WriteResult(true);
            return true;
        }
    }
}
