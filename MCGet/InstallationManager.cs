﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;
using ConsoleTools;

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
        public string? modloader { get; set; }
        public string archivePath { get; set; } = "";
        public string minecraftDir { get; set; } = "";
        public string installationDir { get; set; } = "";
        public bool installationDirWasEmpty { get; set; } = true;

        public string? Id { get; set; }
        public string? modloaderProfile { get; set; }
        public bool isServer { get; set; } = false;
        public List<CustomMod> customMods { get; set; } = new List<CustomMod>();

        public void GenerateId()
        {
            Id = Random.Shared.Next().ToString();
        }
    }

    public class CustomMod
    {
        public string name { get; set; } = "";
        public string? slug { get; set; }
        public string? projectId { get; set; }
        public List<string> files { get; set; } = new List<string>();
    }

    public class InstallationManager
    {
        public InstallationsJson installations = new InstallationsJson();
        public Installation currInstallation = new Installation();
        public string path = "";
        public string filename = "installations.json";

        public delegate void UpdateProgressDelegate(int progress);
        public event UpdateProgressDelegate? updateProgress;

        public void LoadOrCreate(string path)
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

            if (!File.Exists(Path.GetFullPath(Path.Join(path, filename))))
                return null;

            try
            {
                return JsonSerializer.Deserialize<InstallationsJson>(File.ReadAllText(Path.GetFullPath(Path.Join(path, filename))));
            }
            catch (Exception e)
            {
                CTools.WriteError(e.Message);
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
                File.WriteAllText(Path.Join(path, filename), JsonSerializer.Serialize(installations, options));
            }
            catch (Exception e)
            {
                CTools.WriteError(e.Message);
                return false;
            }
            return true;
        }

        public void SetMinecraftPath(string path)
        {
            installations.settings.minecraftPath = path;
        }

        public static string LocalToGlobalPath(string path)
        {
            if (path.Replace("\\", "/").StartsWith("./"))
                path = Program.dir.TrimEnd('\\').TrimEnd('/') + path.Substring(1);
            return path;
        }

        public string GetFullDefaultInstallationPath()
        {
            string path = installations.settings.defaultInstallationPath ?? "";
            return LocalToGlobalPath(path);
        }

        public bool DeleteLauncherProfile(Installation installation)
        {
            if (installation.modloaderProfile == null || installation.modloaderProfile == "")
                return false;
            ProfileHandler ph = new ProfileHandler();
            if (!ph.LoadProfiles(currInstallation.minecraftDir + "/launcher_profiles.json"))
                return false;
            bool result = ph.RemoveProfile(installation.modloaderProfile);
            if (!ph.SaveProfiles(currInstallation.minecraftDir + "/launcher_profiles.json"))
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

        /// <summary>
        /// Generates Id if not yet generated or id is not unique
        /// </summary>
        /// <param name="installation"></param>
        public void EnsureUniqueId(Installation installation)
        {
            while (installation.Id == null || installation.Id == "" || installations.installations.Any((e) => e.Id == installation.Id && e != installation))
            {
                installation.GenerateId();
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

        public List<Installation> GetInstallationsBySlug(string? slug)
        {
            if (slug == null || slug == "")
                return new List<Installation>();
            return installations.installations.FindAll((e) => e.slug == slug);
        }

        public List<Installation> SearchInstallations(string installIdOrSlug, bool startsWith = false)
        {
            if (installIdOrSlug == "")
                return new List<Installation>();
            if (startsWith)
                return installations.installations.FindAll((e) => (e.slug?.ToLower().StartsWith(installIdOrSlug.ToLower()) ?? false) || (e.Id?.StartsWith(installIdOrSlug) ?? false));
            else
                return installations.installations.FindAll((e) => (e.slug?.ToLower().Contains(installIdOrSlug.ToLower()) ?? false) || (e.Id?.Contains(installIdOrSlug) ?? false));
        }

        public void AddInstallation(Installation installation)
        {
            if (!installations.installations.Contains(installation))
            {
                installations.installations.Add(installation);
            }
        }

        public void RemoveInstallation(Installation installation)
        {
            installations.installations.Remove(installation);
        }
    }
}
