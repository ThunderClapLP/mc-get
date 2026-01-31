using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
using ConsoleTools;

namespace MCGet
{
    public class InstallationsJson
    {
        public int fileVersion { get; set; } = 1;
        public Settings settings { get; set; } = new Settings();
        public List<Installation> installations { get; set; } = new List<Installation>();

        public void Load(JsonNode element)
        {
            fileVersion = (int?)element["fileVersion"] ?? 0;
            settings = new Settings();
            settings.Load(element["settings"] ?? new JsonObject());
            installations.Clear();
            foreach (JsonNode? installationJson in element["installations"]?.AsArray() ?? new JsonArray())
            {
                if (installationJson != null)
                {
                    Installation installation = new Installation();
                    installation.Load(installationJson);
                    installations.Add(installation);
                }
            }
        }

        public void Save(JsonNode element)
        {
            element["fileVersion"] = fileVersion;
            JsonNode settingsJson = element["settings"] ?? new JsonObject();
            settings.Save(settingsJson);
            element["settings"] = settingsJson;

            JsonArray installationsJson = new JsonArray();
            foreach (Installation installation in installations)
            {
                JsonNode installationJson = new JsonObject();
                installation.Save(installationJson);
                installationsJson.Add(installationJson);
            }
            element["installations"] = installationsJson;
        }
    }

    public class Settings
    {
        public string minecraftPath { get; set; } = "";
        public string defaultInstallationPath { get; set; } = "./installations";
        public string cfApiUrl { get; set; } = "https://api.curseforge.com/v1"; //full curseforge api url including all static parts
        public string? cfApiKey { get; set; } //curseforge api key

        public void Load(JsonNode element)
        {
            minecraftPath = (string?)element["minecraftPath"] ?? minecraftPath;
            defaultInstallationPath = (string?)element["defaultInstallationPath"] ?? defaultInstallationPath;
            cfApiUrl = (string?)element["cfApiUrl"] ?? cfApiUrl;
            cfApiKey = (string?)element["cfApiKey"];
        }

        public void Save(JsonNode element)
        {
            element["minecraftPath"] = minecraftPath;
            element["defaultInstallationPath"] = defaultInstallationPath;
            element["cfApiUrl"] = cfApiUrl;
            element["cfApiKey"] = cfApiKey;
        }
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

        public void Load(JsonNode element)
        {
            modpackName = (string?)element["modpackName"] ?? modpackName;
            slug = (string?)element["slug"];
            modpackVersion = (string?)element["modpackVersion"];
            mcVersion = (string?)element["mcVersion"];
            modloader = (string?)element["modloader"];
            archivePath = (string?)element["archivePath"] ?? archivePath;
            minecraftDir = (string?)element["minecraftDir"] ?? minecraftDir;
            installationDir = (string?)element["installationDir"] ?? installationDir;
            installationDirWasEmpty = (bool?)element["installationDirWasEmpty"] ?? installationDirWasEmpty;
            Id = (string?)element["Id"];
            modloaderProfile = (string?)element["modloaderProfile"];
            isServer = (bool?)element["isServer"] ?? isServer;
            
            customMods.Clear();
            foreach (JsonNode? customModJson in element["customMods"]?.AsArray() ?? new JsonArray())
            {
                if (customModJson != null)
                {
                    CustomMod customMod = new CustomMod();
                    customMod.Load(customModJson);
                    customMods.Add(customMod);
                }
            }
        }

        public void Save(JsonNode element)
        {
            element["modpackName"] = modpackName;
            element["slug"] = slug;
            element["modpackVersion"] = modpackVersion;
            element["mcVersion"] = mcVersion;
            element["modloader"] = modloader;
            element["archivePath"] = archivePath;
            element["minecraftDir"] = minecraftDir;
            element["installationDir"] = installationDir;
            element["installationDirWasEmpty"] = installationDirWasEmpty;
            element["Id"] = Id;
            element["modloaderProfile"] = modloaderProfile;
            element["isServer"] = isServer;

            JsonArray customModsJson = new JsonArray();
            foreach (CustomMod customMod in customMods)
            {
                JsonNode customModJson = new JsonObject();
                customMod.Save(customModJson);
                customModsJson.Add(customModJson);
                
            }
            element["customMods"] = customModsJson;
        }
    }

    public class CustomMod
    {
        public string name { get; set; } = "";
        public string? slug { get; set; }
        public string? projectId { get; set; }
        public List<string> files { get; set; } = new List<string>();

        public void Load(JsonNode element)
        {
            name = (string?)element["name"] ?? name;
            slug = (string?)element["slug"];
            projectId = (string?)element["projectId"];

            files.Clear();
            foreach (JsonNode? fileJson in element["files"]?.AsArray() ?? new JsonArray())
            {
                if ((string?)fileJson != null)
                {
                    files.Add((string)fileJson!);
                }
            }
        }

        public void Save(JsonNode element)
        {
            element["name"] = name;
            element["slug"] = slug;
            element["projectId"] = projectId;
            JsonArray filesJson = new JsonArray();
            files.ForEach(e => filesJson.Add((JsonNode)e));
            element["files"] = filesJson;
        }
    }

    public class InstallationManager
    {
        public InstallationsJson installations = new InstallationsJson();
        public Installation currInstallation = new Installation();
        public string path = "";
        public string filename = "installations.json";

        public delegate void UpdateProgressDelegate(int progress);
        public event UpdateProgressDelegate? updateProgress;

        private JsonNode jsonRoot = new JsonObject();

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
                jsonRoot = JsonNode.Parse(File.ReadAllText(Path.GetFullPath(Path.Join(path, filename)))) ?? new JsonObject();
                InstallationsJson installationsJson = new InstallationsJson();
                installationsJson.Load(jsonRoot);
                return installationsJson;
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
            jsonRoot = new JsonObject();
            json.Load(jsonRoot);
            this.path = path;

            installations = json;

            return json;
        }

        public bool Save()
        {
            try
            {
                installations.Save(jsonRoot);
                JsonSerializerOptions options = new() { WriteIndented = true };
                File.WriteAllText(Path.Join(path, filename), jsonRoot.ToJsonString(options));
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

        /// <summary>
        /// Sets the given setting to a value
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <returns>true if setting exists</returns>
        /// <exception cref="DirectoryNotFoundException"></exception>
        /// <exception cref="UriFormatException"></exception>
        public bool SetSetting(string name, string value)
        {
            switch (name)
            {
                case "minecraftPath":
                    if (Directory.Exists(value))
                        installations.settings.minecraftPath = value;
                    else
                        throw new DirectoryNotFoundException("Directory \"" + value + "\" does not exist");
                    return true;
                case "defaultInstallationPath":
                    installations.settings.defaultInstallationPath = value;
                    return true;
                case "cfApiUrl":
                    {
                        Uri? uri;
                        if (Uri.TryCreate(value, UriKind.Absolute, out uri) && (uri?.Scheme == Uri.UriSchemeHttp || uri?.Scheme == Uri.UriSchemeHttps))
                            installations.settings.cfApiUrl = uri.AbsoluteUri.TrimEnd('/');
                        else
                            throw new UriFormatException("\"" + value + "\" is not a valid url");
                        return true;
                    }
                case "cfApiKey":
                    installations.settings.cfApiKey = value;
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Resets the given setting to its default value
        /// </summary>
        /// <param name="name"></param>
        /// <returns>true if setting exists</returns>
        public bool UnsetSetting(string name)
        {
            switch (name)
            {
                case "minecraftPath":
                    installations.settings.minecraftPath = new Settings().minecraftPath;
                    return true;
                case "defaultInstallationPath":
                    installations.settings.defaultInstallationPath = new Settings().defaultInstallationPath;
                    return true;
                case "cfApiUrl":
                    installations.settings.cfApiUrl = new Settings().cfApiUrl;
                    return true;
                case "cfApiKey":
                    installations.settings.cfApiKey = new Settings().cfApiKey;
                    return true;
            }
            return false;
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
            if (!ph.LoadProfiles(installation.minecraftDir + "/launcher_profiles.json"))
                return false;
            bool result = ph.RemoveProfile(installation.modloaderProfile);
            if (!ph.SaveProfiles(installation.minecraftDir + "/launcher_profiles.json"))
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
