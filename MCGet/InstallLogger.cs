using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace MCGet
{
    public class InstallLog
    {
        public List<LogFile>? installedMods { get; set; }
        public List<LogFile>? failedMods { get; set; }
        public List<LogFile>? overrides { get; set; }
        public string archiveFile { get; set; } = "";
        public string? minecraftPath { get; set; }
        public string? installationPath { get; set; }
        public string? versionId { get; set; }
        public string? modloaderProfile { get; set; }
    }

    public class LogFile
    {
        public string? path { get; set; }
        public bool overridden { get; set; }
        public string projectId { get; set; } = "";

        public string sha512 { get; set; } = "";

        public LogFile(string path, bool overridden, string projectId, string sha512 = "") { this.path = path; this.overridden = overridden; this.projectId = projectId; this.sha512 = sha512; }
        public LogFile() { }
    }

    public class InstallLogger
    {
        public InstallLog log;
        public string path = "";
        public string filename = "log.Json";

        public delegate void UpdateProgressDelegate(int progress);
        public event UpdateProgressDelegate? updateProgress;
        
        public InstallLogger(string path)
        {
            InstallLog? bl = Load(path) ?? Create(path);
            log = bl;
        }

        public InstallLog? Load(string path)
        {
            this.path = path;

            if (path == "")
                return null;

            if (!File.Exists(Path.GetFullPath(Path.Join(path, filename))))
                return null;

            try
            {
                return JsonSerializer.Deserialize<InstallLog>(File.ReadAllText(Path.GetFullPath(Path.Join(path, filename))));
            }
            catch (Exception)
            {
                
            }
            return null;
        }

        public InstallLog Create(string path)
        {
            InstallLog bl = new InstallLog();
            this.path = path;

            bl.overrides = new List<LogFile>();
            bl.installedMods = new List<LogFile>();
            bl.failedMods = new List<LogFile>();
            bl.minecraftPath = "";

            log = bl;

            try
            {
                if (path != "")
                    Clean();
            }
            catch (Exception)
            {

            }

            return bl;
        }

        public bool Clean()
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                Directory.CreateDirectory(path);
                Directory.CreateDirectory(path + "/mods");
                Directory.CreateDirectory(path + "/overrides");

                log.installedMods?.Clear();
                log.overrides?.Clear();
                log.failedMods?.Clear();

                if (log.installedMods == null)
                    log.installedMods = new List<LogFile>();
                if (log.failedMods == null)
                    log.failedMods = new List<LogFile>();
                if (log.overrides == null)
                    log.overrides = new List<LogFile>();
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public bool Save()
        {
            try
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                File.WriteAllText(Path.Join(path, filename), JsonSerializer.Serialize(log, options));
            }
            catch (Exception)
            {

                return false;
            }
            return true;
        }

        public void SetMinecraftPath(string path)
        {
            log.minecraftPath = path;
        }

        public bool BackopMod(string orgPath, bool overridden)
        {
            if (overridden)
            {
                try
                {
                    File.Copy(orgPath, path + "/mods/" + Path.GetFileName(orgPath), true);
                    log.installedMods?.Add(new LogFile(Path.GetFileName(orgPath), overridden, ""));
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                log.installedMods?.Add(new LogFile(Path.GetFileName(orgPath), overridden, ""));
            }

            return true;
        }

        public bool BackopOverride(string orgPath, bool overridden)
        {
            if (overridden)
            {
                try
                {
                    if (log.minecraftPath != null)
                    {
                        string newPath = path + "/overrides/" + orgPath.Substring(log.minecraftPath.Length);
                        if (!Directory.Exists(Directory.GetParent(newPath)?.FullName))
                            Directory.CreateDirectory(Directory.GetParent(newPath)?.FullName + "");
                        File.Copy(orgPath, newPath, true); ;
                        log.overrides?.Add(new LogFile(orgPath.Substring(log.minecraftPath.Length), overridden, ""));
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
            else
            {
                log.overrides?.Add(new LogFile(orgPath, overridden, ""));
            }

            return true;
        }

        public void AddFailedMod(string projectId)
        {
            if (log.failedMods == null)
                return;
            log.failedMods.Add(new LogFile("", false, projectId));
        }

        public bool RestoreMods()
        {
            if (log.installedMods != null && log.minecraftPath != "")
            {
                if (!Directory.Exists(log.minecraftPath + "/mods/"))
                {
                    Directory.CreateDirectory(log.minecraftPath + "/mods/");
                }

                bool failed = false;
                foreach (LogFile mod in log.installedMods)
                {
                    try
                    {
                        //TODO: organize better. maybe store mods/ dir inside mod.path to make it possible to restore shaders and resourcepacks
                        if (mod.overridden)
                            File.Copy(path + "/mods/" + mod.path, log.minecraftPath + "/mods/" + mod.path, true);
                        else if (File.Exists(log.minecraftPath + "/mods/" + mod.path)) //ignore if doesn't exists
                            File.Delete(log.minecraftPath + "/mods/" + mod.path);

                        updateProgress?.Invoke(0);
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }
                }
                return !failed;
            }
            return false;
        }

        public bool RestoreOverrides()
        {
            if (log.overrides != null)
            {
                bool failed = false;
                foreach (LogFile over in log.overrides)
                {
                    try
                    {
                        if (over.overridden)
                            File.Copy(path + "/overrides/" + over.path, log.minecraftPath + "/" + over.path, true);
                        else if (File.Exists(log.minecraftPath + "/" + over.path)) //ignore if doesn't exists
                            File.Delete(log.minecraftPath + "/" + over.path);
                    }
                    catch (Exception)
                    {
                        failed = true;
                    }

                    updateProgress?.Invoke(0);
                }
                return !failed;
            }
            return false;
        }

        public bool DeleteLauncherProfile()
        {
            if (log.modloaderProfile == null || log.modloaderProfile == "")
                return false;
            ProfileHandler ph = new ProfileHandler();
            ph.LoadProfiles(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json");
            bool result = ph.RemoveProfile(log.modloaderProfile);
            if (!ph.SaveProfiles(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json"))
                result = false;
            return result;
        }

        public void ClearUpdateHandles()
        {
            if (updateProgress == null)
                return;

            foreach (Delegate d in updateProgress.GetInvocationList())
            {
                updateProgress -= (UpdateProgressDelegate)d;
            }
        }

        public bool HasModWithId(string projectId)
        {
            if (log.installedMods == null)
                return false;
            foreach (LogFile mod in log.installedMods)
            {
                if (mod.projectId == projectId)
                    return true;
            }
            return false;
        }

        public bool IsModFailed(string projectId)
        {
            if (log.failedMods == null)
                return false;
            foreach (LogFile mod in log.failedMods)
            {
                if (mod.projectId == projectId)
                    return true;
            }
            return false;
        }

        public LogFile? GetModFromPath(string path)
        {
            if (log.installedMods == null)
                return null;
            foreach (LogFile mod in log.installedMods)
            {
                if (mod.path == path)
                    return mod;
            }
            return null;
        }
    }
}
