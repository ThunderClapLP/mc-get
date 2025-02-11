﻿using ConsoleTools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCGet.Platforms
{
    public class SearchResult
    {
        public bool success = false;
        public List<string> results = new List<string>();
    }

    public class GetProjectResult
    {
        public enum ErrorCode
        {
            None = 0,
            NotFound = 1,
            ConnectionFailed = 2,
        }

        public bool success = false;
        public ErrorCode error = ErrorCode.None;
        public List<string> urls = new List<string>();
        public string name = "";
    }

    public abstract class Platform
    {
        public string name = "";
        protected List<String> downloadedMods = new List<String>();
        public abstract bool InstallDependencies();

        public abstract bool DownloadMods();

        public virtual bool InstallMods()
        {
            string modsDir = Path.GetFullPath(Program.minecraftDir + "/mods");

            CTools.Write("Copying mods");
            ProgressBar bar = new ProgressBar(0, CTools.DockRight());
            bar.fill = true;
            bar.max = downloadedMods.Count;

            if (bar.max == 0)
            {
                bar.max = 1;
                bar.value = 1;
            }

            try
            {
                foreach (string file in downloadedMods)
                {
                    if (Path.GetDirectoryName(Program.minecraftDir + "/" + file) != null && !Directory.Exists(Path.GetDirectoryName(Program.minecraftDir + "/" + file)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Program.minecraftDir + "/" + file)!);
                    }

                    if (file.StartsWith("mods/") && !File.Exists(modsDir + "/" + Path.GetFileName(file)))
                        Program.backup.BackopMod(modsDir + "/" + Path.GetFileName(file), false);

                    File.Move(Program.dir + Program.tempDir + "mods/" + file, Program.minecraftDir + "/" + file, true);
                    bar.value++;
                    bar.Update();

                }
            }
            catch (Exception e)
            {
                bar.Clear();
                CTools.WriteResult(false);
                CTools.WriteLine(e.Message);
                CTools.WriteLine(e.StackTrace);
                Program.RevertChanges();
                return false;
            }
            bar.Clear();
            CTools.WriteResult(true);
            return true;
        }

    }
}
