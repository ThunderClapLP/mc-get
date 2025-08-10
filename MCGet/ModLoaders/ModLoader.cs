using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleTools;

namespace MCGet.ModLoaders
{
    public abstract class ModLoader
    {
        public string javaPath = "";
        public abstract bool Install(string minecraftVersion, string loaderVersion);

        public bool DownloadLoader(string url, Spinner spinner, string fileName = "") {

            CTools.CursorLeft = 0;
            CTools.WriteLine("");
            if (CTools.CursorTop > 0)
                CTools.CursorTop -= 1;

            if (fileName == "")
                fileName = Path.GetFileName(url);

            spinner.top = CTools.CursorTop;
            spinner.msg = "Downloading " + fileName + " ";

            if (Networking.DownloadFile(url, Program.dir + Program.tempDir + fileName, spinner))
            {
                CTools.WriteResult(true, spinner);
            }
            else
            {
                //failed
                CTools.WriteResult(false, spinner);
                return false;
            }

            if (Program.DownloadJavaIfNotPresent())
            {
                if (!System.OperatingSystem.IsMacOS())
                    javaPath = Program.dir + "/java/jdk-21.0.6+7-jre/bin/";
                else
                    javaPath = Program.dir + "/java/jdk-21.0.6+7-jre/Contents/Home/bin/";
            }

            return true;
        }

        public bool RunLoaderInstaller(Process proc, Spinner spinner, string loaderName) {
            try
            {
                //perform the installation
                proc.Start();

                spinner.top = CTools.CursorTop;
                spinner.msg = "Installing " + loaderName;

                Task<string> logTask = proc.StandardOutput.ReadToEndAsync(); //read the output because forge and neoforge block otherwise
                Task<string> errorLogTask = proc.StandardError.ReadToEndAsync();
                while (!proc.HasExited) {
                    spinner.Update();
                    proc.WaitForExit(100);
                }

                errorLogTask.Wait();

                if (proc.ExitCode != 0)
                {
                    CTools.WriteResult(false, spinner);
                    CTools.WriteLine(errorLogTask.Result + proc.StandardError.ReadToEnd());
                    return false;
                }
            }
            catch (Exception)
            {
                CTools.WriteResult(false, spinner);
                CTools.WriteError("Installing " + loaderName + " failed - Is java installed?");
                return false;
            }

            CTools.WriteResult(true, spinner);
            return true;
        }
    }
}
