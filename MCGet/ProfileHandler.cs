using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCGet
{
    public class ProfileHandler
    {
        public enum SnapshotNumber {FIRST = 0, SECOND = 1};
        private List<string>[] Snapshots = {new List<string>(), new List<string>()};
        private JsonNode? profileJson;

        /// <summary>
        /// Creates a snapshot of the given Minecraft launcher json file
        /// </summary>
        /// <param name="profilePath">Path to minecraft launcher json</param>
        /// <param name="number">Number of snapshot</param>
        /// <returns>True if successful</returns>
        public bool CreateSnapshot(string profilePath, SnapshotNumber number)
        {
            Snapshots[(int)number].Clear();

            try
            {
                JsonDocument json = JsonDocument.Parse(File.ReadAllText(profilePath));
                foreach (JsonProperty profile in json.RootElement.GetProperty("profiles").EnumerateObject())
                {
                    Snapshots[(int)number].Add(profile.Name);
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Computes the difference between the two collected snapshots.<br/>
        /// Both snapshots need to be taken first
        /// </summary>
        /// <returns>List of new Profiles in snapshot two</returns>
        public List<string> ComputeDifference()
        {
            List<string> newProfiles = new List<string>();
            foreach (string profile in Snapshots[1])
            {
                if (!Snapshots[0].Any(p => p == profile)) //check if fist snapshot contains the profile from the second snapshot
                    newProfiles.Add(profile);
            }
            return newProfiles;
        }

        /// <summary>
        /// Loads all profiles
        /// </summary>
        /// <param name="profilePath">Path to minecraft launcher json</param>
        /// <param name="id"></param>
        /// <returns>True if successful</returns>
        public bool LoadProfiles(string profilePath = "")
        {
            if (profilePath == "")
                profilePath = Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json";
            try
            {
                profileJson = JsonNode.Parse(File.ReadAllText(profilePath));
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Saves modified json
        /// </summary>
        /// <param name="profilePath">Path to minecraft launcher json</param>
        /// <returns>True if successful</returns>
        public bool SaveProfiles(string profilePath)
        {
            if (profileJson == null)
                return false;
            try
            {
                File.WriteAllText(profilePath, profileJson?.ToJsonString(new JsonSerializerOptions() {WriteIndented = true}));
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets profiles by given loaderType and loaderVersion
        /// </summary>
        /// <param name="loaderType"></param>
        /// <param name="loaderVersion"></param>
        /// <returns>List of loaders (should be one but may be more in edge cases)</returns>
        public List<string> GetProfilesByLoaderVersion(string loaderType, string loaderVersion)
        {
            List<string> ret = new List<string>();

            if (profileJson != null)
            {
                try
                {
                    foreach (KeyValuePair<string, JsonNode?> profile in profileJson?["profiles"]?.AsObject().AsEnumerable() ?? Array.Empty<KeyValuePair<string, JsonNode?>>())
                    {
                        string? lastVersion = ((string?)profile.Value?["lastVersionId"])?.ToLower();
                        if (lastVersion != null)
                            if (lastVersion.StartsWith(loaderType.ToLower()) && lastVersion.Contains(loaderVersion.ToLower()))
                                ret.Add(profile!.Key);
                    }
                }
                catch(Exception) {}
            }

            return ret;
        }

        /// <summary>
        /// Gets the name of the profile given by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>name of the profile</returns>
        public string? GetProfileName(string id)
        {
            JsonNode? profile = profileJson?["profiles"]?[id];
            return (string?)profile?["name"];
        }

        /// <summary>
        /// Sets the name of the profile given by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="newName"></param>
        /// <param name="unique"></param>
        /// <returns>True if successful</returns>
        public bool SetProfileName(string id, string newName, bool unique = false)
        {
            JsonNode? profile = profileJson?["profiles"]?[id];

            if (profile == null)
                return false;

            if (unique)
            {
                bool contains;
                int count = 0;
                string name = newName;
                do
                {
                    contains = false;
                    foreach (KeyValuePair<string, JsonNode?> prof in profileJson?["profiles"]?.AsObject().AsEnumerable() ?? Array.Empty<KeyValuePair<string, JsonNode?>>())
                    {
                        if (prof.Value != null)
                        {
                            if ((string?)prof.Value["name"] == name)
                            {
                                contains = true;
                                count++;
                                name = newName + "(" + count + ")";
                            }
                        }
                    }
                } while (contains);
                newName = name;
            }
            profile!["name"] = newName;
            return true;
            
        }

        /// <summary>
        /// Set id of the profile given by the old id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="newId"></param>
        /// <returns>True if successful</returns>
        public bool SetProfieId(string id, string newId)
        {
            JsonNode? profile = profileJson?["profiles"]?[id];
            JsonNode? profileList = profileJson?["profiles"];

            if (profileList == null || profile == null)
                return false;

            profileList?.AsObject().Remove(id);
            profileList?.AsObject().Add(newId, profile);

            return true;
        }

        /// <summary>
        /// Set the directory where the installation is stored
        /// </summary>
        /// <param name="id"></param>
        /// <param name="path"></param>
        /// <returns>True if successful</returns>
        public bool SetProfileGameDirectory(string id, string path)
        {
            JsonNode? profile = profileJson?["profiles"]?[id];

            if (profile == null)
                return false;

            profile["gameDir"] = path;

            return true;
        }

        /// <summary>
        /// Completely remove profile given by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>True if successful</returns>
        public bool RemoveProfile(string id)
        {
            JsonNode? profile = profileJson?["profiles"]?[id];
            JsonNode? profileList = profileJson?["profiles"];

            if (profileList == null || profile == null)
                return false;

            return profileList.AsObject().Remove(id);
        }


    }
}