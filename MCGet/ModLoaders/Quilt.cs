using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ConsoleTools;

namespace MCGet.ModLoaders
{
    public class Quilt : ModLoader
    {
        static string url = "https://maven.quiltmc.org/repository/release/org/quiltmc/quilt-installer/0.9.2/quilt-installer-0.9.2.jar";
        public override bool Install(string minecraftVersion, string loaderVersion)
        {
            if (loaderVersion == "" || minecraftVersion == "")
            {
                //no version given
                CTools.WriteError("Could not install Quilt");
                return false;
            }

            loaderVersion = loaderVersion.Replace("quilt-", ""); //make sure version has the correct format
            Spinner spinner = new Spinner(CTools.CursorTop);

            if (!DownloadLoader(url, spinner))
                return false;

            Process quilt = new Process();
            quilt.StartInfo.FileName = javaPath + "java";
            quilt.StartInfo.Arguments = "-jar \"" + Program.dir + Program.tempDir + Path.GetFileName(url) + "\" install client " + minecraftVersion + " " + loaderVersion + " --install-dir=\"" + Program.minecraftDir + "\"";
            quilt.StartInfo.WorkingDirectory = Program.dir + Program.tempDir;

            quilt.StartInfo.RedirectStandardOutput = true;
            quilt.StartInfo.RedirectStandardError = true;
            quilt.StartInfo.RedirectStandardInput = true;
            return RunLoaderInstaller(quilt, spinner, "Quilt");
        }
    }
}
