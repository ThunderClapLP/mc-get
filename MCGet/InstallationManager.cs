using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace MCGet
{
    public class InstallationsJson
    {
        public int fileVersion { get; set; } = 1;
        public Settings settings { get; set; } = new Settings();
        public List<Installation> installations { get; set; } = new List<Installation>();
    }

    public class Settings
    {
        public string minecraftPath { get; set; } = "";
        public string defaultInstallationPath { get; set; } = "./installations";
    }
    public class Installation
    {
        public string modpackName { get; set; } = "";
        public string? slug { get; set; }
        public string? modpackVersion { get; set; }
        public string? mcVersion { get; set; }
        public string archiveFile { get; set; } = "";
        public string? minecraftPath { get; set; }
        public string? installationPath { get; set; }

        public string? modloaderProfileId { get; set; }
        public string? modloaderProfile { get; set; }
        public List<CustomMod>? customMods { get; set; }
    }

    public class CustomMod
    {
        public string name { get; set; } = "";
        public string? slug { get; set; }
        public string? projectId { get; set; }
        public string[]? files { get; set; }
    }

    public class InstallationManager
    {
        public InstallationsJson installations;
        public string path = "";
        public string filename = "installations.json";

        public delegate void UpdateProgressDelegate(int progress);
        public event UpdateProgressDelegate? updateProgress;
        
        public InstallationManager(string path)
        {
            InstallationsJson? json = Load(path);

            if (json == null)
            {
                json = Create(path);
            }

            installations = json;
        }

        public InstallationsJson? Load(string path)
        {
            this.path = path;

            if (path == "")
                return null;

            if (!File.Exists(Path.GetFullPath(path + filename)))
                return null;

            try
            {
                return JsonSerializer.Deserialize<InstallationsJson>(File.ReadAllText(Path.GetFullPath(path + filename)));
            }
            catch (Exception)
            {
                
            }
            return null;
        }

        public InstallationsJson Create(string path)
        {
            InstallationsJson json = new InstallationsJson();
            this.path = path;

            installations = json;

            return json;
        }

        public bool Save()
        {
            try
            {
                JsonSerializerOptions options = new() { WriteIndented = true };
                File.WriteAllText(path + filename, JsonSerializer.Serialize(installations, options));
            }
            catch (Exception)
            {

                return false;
            }
            return true;
        }

        public void SetMinecraftPath(string path)
        {
            installations.settings.minecraftPath = path;
        }

        public string GetFullDefaultInstallationPath()
        {
            string path = installations.settings.defaultInstallationPath ?? "";
            if (path.Replace("\\", "/").StartsWith("./"))
                path = Program.dir + path.Substring(1);
            return path;
        }

        public bool DeleteLauncherProfile(Installation installation)
        {
            if (installation.modloaderProfile == null || installation.modloaderProfile == "")
                return false;
            ProfileHandler ph = new ProfileHandler();
            ph.LoadProfiles(Program.minecraftDir + "/launcher_profiles.json");
            bool result = ph.RemoveProfile(installation.modloaderProfile);
            if (!ph.SaveProfiles(Program.minecraftDir + "/launcher_profiles.json"))
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

        public bool HasCustomModWithId(Installation installation, string projectId)
        {
            if (installation.customMods == null)
                return false;
            foreach (CustomMod mod in installation.customMods)
            {
                if (mod.projectId == projectId)
                    return true;
            }
            return false;
        }
    }
}
