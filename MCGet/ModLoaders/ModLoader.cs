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
        public abstract bool Install(String minecraftVersion, String loaderVersion);

        public bool DownloadLoader(String url, Spinner spinner) {

            CTools.CursorLeft = 0;
            CTools.WriteLine("");
            CTools.CursorTop -= 1;

            spinner.top = CTools.CursorTop;

            CTools.Write("Downloading " + Path.GetFileName(url) + " ");

            spinner.Update();

            if (Networking.DownloadFile(url, Program.dir + Program.tempDir + Path.GetFileName(url), spinner))
            {
                CTools.WriteResult(true);
            }
            else
            {
                //failed
                CTools.WriteResult(false);
                return false;
            }

            if (Program.DownloadJavaIfNotPresent())
            {
                javaPath = Program.dir + "/java/jdk-21.0.6+7-jre/bin/";
            }

            return true;
        }

        public bool RunLoaderInstaller(Process proc, Spinner spinner, String loaderName) {
            try
            {
                //perform the installation
                proc.Start();

                CTools.Write("Installing " + loaderName);
                spinner.top = CTools.CursorTop;

                Task<String> logTask = proc.StandardOutput.ReadToEndAsync(); //read the output because forge and neoforge block otherwise
                Task<String> errorLogTask = proc.StandardError.ReadToEndAsync();
                while (!proc.HasExited) {
                    spinner.Update();
                    proc.WaitForExit(100);
                }

                errorLogTask.Wait();

                if (proc.ExitCode != 0)
                {
                    //Console.WriteLine(quilt.StandardOutput.ReadToEnd());
                    CTools.WriteResult(false);
                    CTools.WriteLine(errorLogTask.Result + proc.StandardError.ReadToEnd());
                    return false;
                }
            }
            catch (Exception)
            {
                CTools.WriteResult(false);
                CTools.WriteError("Installing " + loaderName + " failed - Is java installed?");
                return false;
            }

            CTools.WriteResult(true);
            return true;
        }
    }
}
