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
        static string serverUrl = "https://meta.fabricmc.net/v2/versions/loader/{MC_VERSION}/{LOADER_VERSION}/1.0.1/server/jar";
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

            //Alternative server install
            // if (Program.insManager.currInstallation.isServer)
            // {
            //     string serverFullUrl = url.Replace("{MC_VERSION}", minecraftVersion).Replace("{LOADER_VERSION}", loaderVersion);
            //     string fileName = "fabric-server-mc.{MC_VERSION}-loader.{LOADER_VERSION}-launcher.1.0.1.jar"
            //         .Replace("{MC_VERSION}", minecraftVersion).Replace("{LOADER_VERSION}", loaderVersion);
            //     if (DownloadLoader(serverFullUrl, spinner, fileName)) //java -Xmx2G -jar fabric-server-mc.1.21.4-loader.0.16.10-launcher.1.0.1.jar nogui
            //     {
            //         try
            //         {
            //             File.Copy(Program.dir + Program.tempDir + fileName, Program.minecraftDir);
            //         }
            //         catch {}
            //     }
            // }

            if (!DownloadLoader(url, spinner))
                return false;

            //perform the installation
            Process fabric = new Process();
            fabric.StartInfo.FileName = javaPath + "java";
            if (!Program.insManager.currInstallation.isServer)
                fabric.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" client -mcversion " + minecraftVersion + " -loader" + loaderVersion + " -dir \"" + Program.insManager.currInstallation.minecraftDir + "\"";
            else
                fabric.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" server -mcversion " + minecraftVersion + " -loader" + loaderVersion + " -dir \"" + InstallationManager.LocalToGlobalPath(Program.insManager.currInstallation.installationDir) + "\" -downloadMinecraft";
            fabric.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

            fabric.StartInfo.RedirectStandardOutput = true;
            fabric.StartInfo.RedirectStandardError = true;
            fabric.StartInfo.RedirectStandardInput = true;
            return RunLoaderInstaller(fabric, spinner, "Fabric");

        }
    }
}
