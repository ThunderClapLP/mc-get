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
    public class Forge : ModLoader
    {
        static string url = "https://files.minecraftforge.net/maven/net/minecraftforge/forge/{VERSION}-{FORGE_VERSION}/forge-{VERSION}-{FORGE_VERSION}-installer.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                CTools.WriteError("Could not install forge");
                return false;
            }

            loaderVersion = loaderVersion.Replace("forge-", "");
            //catch case that the forge version is already complete
            if (loaderVersion.Contains("-"))
            {
                minecraftVersion = loaderVersion.Split("-")[0];
                loaderVersion = loaderVersion.Split("-")[1];
            }

            string forgeFullUrl = url.Replace("{VERSION}", minecraftVersion).Replace("{FORGE_VERSION}", loaderVersion);
            Spinner spinner = new Spinner(CTools.CursorTop);

            if (!DownloadLoader(forgeFullUrl, spinner))
                return false;

            try
            {
                //perform installation
                Process forge = new Process();
                forge.StartInfo.FileName = javaPath + "java";
                if (!Program.cServer)
                    forge.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(forgeFullUrl) + "\"";
                else
                    forge.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(forgeFullUrl) + "\" --InstallServer \"" + Program.minecraftDir + "\"";
                forge.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

                //NOTE: forge installer somehow fails with redirected output
                //forge.StartInfo.RedirectStandardOutput = true;
                //forge.StartInfo.RedirectStandardError = true;
                //forge.StartInfo.RedirectStandardInput = true;
                //Task<String> logTask = forge.StandardOutput.ReadToEndAsync(); //read the output because forge and neoforge block otherwise
                //Task<String> errorLogTask = forge.StandardError.ReadToEndAsync();
                forge.Start();

                CTools.Write("Please follow the instructions of the forge Installer");

                forge.WaitForExit();
            }
            catch (Exception)
            {
                CTools.WriteResult(false);
                CTools.WriteError("Installing Forge failed - Is java installed?");
                return false;
            }

            CTools.WriteResult(true);

            return true;
        }
    }
}
