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
    public class Forge : ModLoader
    {
        static string url = "https://files.minecraftforge.net/maven/net/minecraftforge/forge/{VERSION}-{FORGE_VERSION}/forge-{VERSION}-{FORGE_VERSION}-installer.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                ConsoleTools.WriteError("Could not install forge");
                return false;
            }

            loaderVersion = loaderVersion.Replace("forge-", "");
            string forgeFullUrl = url.Replace("{VERSION}", minecraftVersion).Replace("{FORGE_VERSION}", loaderVersion);
            Console.WriteLine(forgeFullUrl);

            Console.CursorLeft = 0;
            Console.WriteLine("");
            Console.CursorTop -= 1;

            Spinner spinner = new Spinner(Console.CursorTop);
            spinner.top = Console.CursorTop;

            Console.Write("Downloading " + Path.GetFileName(forgeFullUrl) + " ");

            spinner.Update();

            if (Networking.DownloadFile(forgeFullUrl, Program.dir + Program.tempDir + Path.GetFileName(url), spinner))
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
                //perform installation
                Process forge = new Process();
                forge.StartInfo.FileName = javaPath + "java";
                forge.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(forgeFullUrl) + "\"";
                forge.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

                //NOTE: forge installer somehow fails with redirected output
                //forge.StartInfo.RedirectStandardOutput = true;
                //forge.StartInfo.RedirectStandardError = true;
                //forge.StartInfo.RedirectStandardInput = true;
                forge.Start();

                Console.Write("Please follow the instructions of the forge Installer");

                forge.WaitForExit();
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
                ConsoleTools.WriteError("Installing Forge failed - Is java installed?");
                return false;
            }

            ConsoleTools.WriteResult(true);

            return true;
        }
    }
}
