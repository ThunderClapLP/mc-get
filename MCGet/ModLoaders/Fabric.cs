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
using ConsoleTools;

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
                CTools.WriteError("Could not install fabric");
                return false;
            }

            loaderVersion = loaderVersion.Replace("fabric-", ""); //make sure version has the correct format
            Spinner spinner = new Spinner(CTools.CursorTop);

            if (!DownloadLoader(url, spinner))
                return false;

            //perform the installation
            Process fabric = new Process();
            fabric.StartInfo.FileName = javaPath + "java";
            if (!Program.cServer)
                fabric.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" client -mcversion " + minecraftVersion + " -loader" + loaderVersion + " -dir \"" + Program.minecraftDir + "\"";
            else
                fabric.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" server -mcversion " + minecraftVersion + " -loader" + loaderVersion + " -dir \"" + Program.minecraftDir + "\"";
            fabric.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

            fabric.StartInfo.RedirectStandardOutput = true;
            fabric.StartInfo.RedirectStandardError = true;
            fabric.StartInfo.RedirectStandardInput = true;
            return RunLoaderInstaller(fabric, spinner, "Fabric");

        }
    }
}
