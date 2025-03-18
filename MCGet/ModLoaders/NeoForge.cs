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
    public class NeoForge : ModLoader
    {
        static string url = "https://maven.neoforged.net/releases/net/neoforged/neoforge/{FORGE_VERSION}/neoforge-{FORGE_VERSION}-installer.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                CTools.WriteError("Could not install forge");
                return false;
            }

            loaderVersion = loaderVersion.Replace("neoforge-", "");

            string forgeFullUrl = url.Replace("{FORGE_VERSION}", loaderVersion);
            Spinner spinner = new Spinner(CTools.CursorTop);

            if (!DownloadLoader(forgeFullUrl, spinner))
            {
                //try beta version if download fails. modrinth doesn't seem to specify if it is a beta version
                forgeFullUrl = url.Replace("{FORGE_VERSION}", loaderVersion + "-beta");
                if (!DownloadLoader(forgeFullUrl, spinner))
                    return false;
            }

            //perform installation
            Process neoforge = new Process();
            neoforge.StartInfo.FileName = javaPath + "java";
            if (!Program.insManager.currInstallation.isServer)
                neoforge.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(forgeFullUrl) + "\" --install-client \"" + Program.insManager.currInstallation.minecraftDir + "\"";
            else
                neoforge.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(forgeFullUrl) + "\" --InstallServer \"" + InstallationManager.LocalToGlobalPath(Program.insManager.currInstallation.installationDir) + "\"";
            neoforge.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

            neoforge.StartInfo.RedirectStandardOutput = true;
            neoforge.StartInfo.RedirectStandardError = true;
            neoforge.StartInfo.RedirectStandardInput = true;
            return RunLoaderInstaller(neoforge, spinner, "NeoForge");
        }
    }
}
