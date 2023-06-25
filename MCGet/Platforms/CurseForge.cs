using MCGet.ModLoaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MCGet.Platforms
{
    public class CurseForge : Platform
    {
        static string proxy = "";
        static string cfurl = "";
        public override bool DownloadMods()
        {
            List<JsonElement> failedMods = new List<JsonElement>();
            int tryCount = 0;

            if (Program.manifestDoc != null && Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() > 0)
            {
                ProgressBar bar = new ProgressBar(0, ConsoleTools.DockRight());
                bar.fill = true;
                bar.max = Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength();

                Spinner spinner = new Spinner(Console.CursorTop);

                int failcount = 0;
                while ((tryCount < 4 && failcount > 0) || tryCount == 0)
                {
                    if (tryCount > 0)
                    {
                        bar.value = 0;
                        bar.max = failedMods.Count;
                        bar.Update();

                        Console.Write("Retrying failed Mods. (Try " + tryCount + " / " + "3)");

                        bar.Update();

                        //spinner.top++;
                        for (int r = 0; r < 30; r++)
                        {
                            Thread.Sleep(200);
                            spinner.Update();
                        }

                        spinner.Clean();
                        Console.WriteLine();
                        bar.Update();
                    }

                    failcount = 0;
                    foreach (JsonElement file in Program.manifestDoc.RootElement.GetProperty("files").EnumerateArray())
                    {
                        if (tryCount <= 0 || failedMods.Contains(file))
                        {
                            if (failedMods.Contains(file))
                                failedMods.Remove(file);

                            if (!Program.cFixMissing || Program.backup.IsModFailed(file.GetProperty("projectID").ToString() + ""))
                            {
                                if (!DownloadMod(file.GetProperty("projectID").ToString(), file.GetProperty("fileID").ToString(), Program.dir + Program.tempDir + "mods/", spinner))
                                {
                                    failedMods.Add(file);
                                    failcount++;
                                }
                            }
                            bar.value++;
                            bar.Update();
                        }
                    }
                    tryCount++;
                }
                bar.Clear();

                foreach (JsonElement file in failedMods)
                {
                    try
                    {
                        if (file.GetProperty("projectID").ToString() != null)
                            Program.backup.AddFailedMod(file.GetProperty("projectID").ToString() + "");
                    }
                    catch (Exception) { }
                }

                ConsoleTools.ClearLine();
                Console.Write("Download finished!");
                if (failcount > 0)
                {
                    if (!ConsoleTools.ConfirmDialog(" " + failcount + " / " + Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() + " mods failed. Continue?", true))
                    {
                        Program.RevertChanges();
                    }
                }
                Console.WriteLine();

            }
            else
            {
                ConsoleTools.WriteError("Mainifest does not include any files");
                Program.RevertChanges();
                return false;
            }
            return true;
        }

        static bool DownloadMod(string projectId, string fileId, string destination, Spinner? spinner = null)
        {
            ConsoleTools.ClearLine();
            Console.WriteLine("CURSEFORGE DOWNLOAD IS PROHIBITED");
            return false;
        }

        public override bool InstallDependencies()
        {
            //get modloader
            string? modloaderVersion = "";
            try
            {
                if (Program.manifestDoc != null)
                {
                    foreach (JsonElement loader in Program.manifestDoc.RootElement.GetProperty("minecraft").GetProperty("modLoaders").EnumerateArray())
                    {
                        if (loader.GetProperty("primary").GetBoolean())
                        {
                            modloaderVersion = loader.GetProperty("id").GetString();
                        }
                    }
                }
            }
            catch { }

            if (modloaderVersion == null)
            {
                ConsoleTools.WriteError("Could not find a modloader");
                return false;
            }

            if (modloaderVersion.StartsWith("forge"))
                return new Forge().Install(Program.manifestDoc?.RootElement.GetProperty("minecraft").GetProperty("version").GetString() ?? "", modloaderVersion);
            else if (modloaderVersion.StartsWith("fabric"))
                return new Fabric().Install(Program.manifestDoc?.RootElement.GetProperty("minecraft").GetProperty("version").GetString() ?? "", modloaderVersion);

            ConsoleTools.WriteError("Modloader is not compatible");
            return false;
        }

        public override bool InstallMods()
        {
            //prepare
            string modsDir = Path.GetFullPath(Program.minecraftDir + "/mods");

            if (!Directory.Exists(modsDir))
            {
                Console.Write("Creating mods directory");
                try
                {
                    Directory.CreateDirectory(modsDir);
                }
                catch (Exception)
                {
                    ConsoleTools.WriteResult(false);
                    Program.RevertChanges();
                    return false;
                }
                ConsoleTools.WriteResult(true);
            }

            //perform copy
            Console.Write("Copying mods");
            ProgressBar bar = new ProgressBar(0, ConsoleTools.DockRight());
            bar.fill = true;
            bar.max = Directory.GetFiles(Program.dir + Program.tempDir + "mods/").Length;

            if (bar.max == 0)
            {
                bar.max = 1;
                bar.value = 1;
            }

            bar.Update();
            try
            {
                foreach (string file in Directory.GetFiles(Program.dir + Program.tempDir + "mods/"))
                {
                    if (!File.Exists(modsDir + "/" + Path.GetFileName(file)))
                        Program.backup.BackopMod(modsDir + "/" + Path.GetFileName(file), false);

                    File.Copy(file, modsDir + "/" + Path.GetFileName(file), true);
                    bar.value++;
                    bar.Update();
                }
            }
            catch (Exception)
            {
                bar.Clear();
                ConsoleTools.WriteResult(false);
                Program.RevertChanges();
                return false;
            }
            bar.Clear();
            ConsoleTools.WriteResult(true);

            return true;
        }
    }
}
