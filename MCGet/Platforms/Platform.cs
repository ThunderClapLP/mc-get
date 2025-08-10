using ConsoleTools;
using MCGet.ModLoaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MCGet.Platforms
{
    public enum ProjectType
    {
        Invalid = 0,
        Modpack = 1,
        Mod = 2,
    }

    public class SearchResult
    {

        public enum ErrorCode
        {
            None = 0,
            ConnectionFailed = 2,
            ConnectionRefused = 3,
            Gone = 4,
        }
        public bool success = false;
        public ErrorCode error = ErrorCode.None;
        public List<string> results = new List<string>();
    }

    public class GetProjectResult
    {
        public enum ErrorCode
        {
            None = 0,
            NotFound = 1,
            ConnectionFailed = 2,
            ConnectionRefused = 3,
            Gone = 4,
        }

        public bool success = false;
        public ErrorCode error = ErrorCode.None;
        public List<string> urls = new List<string>();
        public string name = "";
        public string slug = "";
        public string loader = "";
        public Type platformType;
        public ProjectType projectType = ProjectType.Invalid;

        public GetProjectResult(Type platformType)
        {
            this.platformType = platformType;
        }
    }

    public abstract class Platform
    {
        public string name = "";
        protected List<string> downloadedMods = new List<string>();
        public abstract bool InstallDependencies();

        public abstract bool DownloadMods();

        public virtual bool InstallMods()
        {
            string modsDir = Path.GetFullPath(Program.insManager.currInstallation.installationDir + "/mods");
            string absDestPath = InstallationManager.LocalToGlobalPath(Program.insManager.currInstallation.installationDir);

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
                    if (Path.GetDirectoryName(absDestPath + "/" + file) != null && !Directory.Exists(Path.GetDirectoryName(absDestPath + "/" + file)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(absDestPath + "/" + file)!);
                    }

                    if (file.StartsWith("mods/") && !File.Exists(modsDir + "/" + Path.GetFileName(file)))
                        Program.backup.BackopMod(modsDir + "/" + Path.GetFileName(file), false);

                    File.Move(Program.dir + Program.tempDir + "mods/" + file, absDestPath + "/" + file, true);
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

        /// <summary>
        /// Installs the modloader and installs or updates the launcher profile
        /// </summary>
        /// <returns>true if successful</returns>
        public virtual bool InstallModloader(ModLoader? modloader, string modloaderVersion)
        {
            ProfileHandler ph = new ProfileHandler();
            ph.CreateSnapshot(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.FIRST);

            bool success = modloader?.Install(Program.insManager.currInstallation.mcVersion ?? "", modloaderVersion) ?? false;

            ph.CreateSnapshot(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.SECOND);

            if (Program.insManager.currInstallation.isServer)
                return success; //returned true before. why?

            ph.LoadProfiles(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json");
            //remove old profile
            string? oldProfileName = null;
            if (Program.modifyExisting && Program.insManager.currInstallation.modloaderProfile != null)
            {
                oldProfileName = ph.GetProfileName(Program.insManager.currInstallation.modloaderProfile);
                ph.RemoveProfile(Program.insManager.currInstallation.modloaderProfile);
            }

            //Get new profile by comparing the profile list from before with the one from after the modloader install. Does nothing if the modloader profile already existed before
            string newProfile = ph.ComputeDifference().FirstOrDefault() ?? "";
            if (newProfile == "") //try by version if difference failed. Installer propably overwrote a profile.
                newProfile = ph.GetProfilesByLoaderVersion(modloaderVersion.Split("-")[0], modloaderVersion.Split("-")[1]).FirstOrDefault("");

            if (newProfile != "")
            {
                //no error checks at the moment
                if (Program.modifyExisting && oldProfileName != null)
                {
                    ph.SetProfileName(newProfile, oldProfileName, true); //use original profile name
                }
                else if (this.name != "")
                    ph.SetProfileName(newProfile, this.name, true); //use modpack name as profile name

                ph.SetProfileGameDirectory(newProfile, InstallationManager.LocalToGlobalPath(Program.insManager.currInstallation.installationDir));
                string newId = Program.insManager.currInstallation.Id ?? new Random().Next().ToString();
                ph.SetProfieId(newProfile, newId);
                ph.SaveProfiles(Program.insManager.currInstallation.minecraftDir + "/launcher_profiles.json");
                Program.backup.log.modloaderProfile = newId;
                Program.insManager.currInstallation.modloaderProfile = newId;
            }

            return success;
        }

    }
}
