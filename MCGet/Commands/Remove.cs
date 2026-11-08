using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ConsoleTools;

namespace MCGet.Commands
{
    public class RemoveParams : CommandParams
    {
        public enum Type
        {
            Invalid,
            Install,
            Mod
        }

        public Type type { get; }
        public string installationSearch { get; }
        public string modSearch { get; }
        public RemoveParams(string[] parameters, Type type, string? installationSearch, string? modSearch) : base(parameters)
        {
            this.type = type;
            this.installationSearch = installationSearch ?? "";
            this.modSearch = modSearch ?? "";
        }
    }
    public class Remove : Command<RemoveParams>
    {
        public Remove(Flags flags, Spinner spinner, InstallationManager insManager) : base(flags, spinner, insManager) {}

        public override void Run(RemoveParams listParams)
        {
            List<Installation> installationSerchResult = listParams.installationSearch != "" ? insManager.SearchInstallations(listParams.installationSearch, true) : insManager.installations.installations;
            if (insManager.currInstallation.installationDir != "") //filter by --path
                installationSerchResult = installationSerchResult.FindAll((e) => InstallationManager.LocalToGlobalPath(e.installationDir).Replace("\\", "/").StartsWith(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir).Replace("\\", "/")));
            Installation? ins = null;
            if (installationSerchResult.Count == 1)
            {
                ins = installationSerchResult[0];
            }
            else if (installationSerchResult.Count > 1)
            {
                ProfileHandler ph = new ProfileHandler();
                string profilePath = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                if (profilePath != "")
                    ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                CTools.WriteLine("    " + CTools.LimitText("ID", int.MaxValue.ToString().Length, true) + " | ProfileName | slug | DirectoryName");
                int insRes = CTools.ListDialog("Choose installation",
                    installationSerchResult.Select((e) => {
                        if (profilePath != e.minecraftDir)
                        {
                            profilePath = e.minecraftDir;
                            ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                        }
                        return CTools.LimitText(e.Id ?? "??", int.MaxValue.ToString().Length, true) + " | " + (ph.GetProfileName(e.modloaderProfile ?? "") ?? "??") + " | " + (e.slug ?? "??") + " | " + e.installationDir.Replace("\\", "/").Split("/").Last();
                    }));
                if (insRes < 0)
                {
                    CTools.WriteError("User input is required!");
                    Environment.Exit(1);
                }
                ins = installationSerchResult[insRes];
            }
            else
            {
                CTools.WriteLine("No installation matches your search query");
            }

            if (ins != null)
            {
                if (listParams.type == RemoveParams.Type.Install)
                {
                    //remove installation
                    CTools.WriteError("All saved worlds will be lost!", 1);
                    if (flags.Silent || CTools.ConfirmDialog("Are you sure to permanently remove installation \"" + ins.modpackName + "\" (" + ins.Id + ") from your disk?", false))
                    {
                        CTools.Write("Removing installation");
                        try
                        {
                            if (!ins.installationDirWasEmpty)
                            {
                                insManager.RemoveInstallation(ins);
                                CTools.WriteResult(true);
                                CTools.WriteError("The installation directory was not empty on installation. You need to remove the files manually.", 1);
                                CTools.WriteLine("Dir: " + InstallationManager.LocalToGlobalPath(ins.installationDir));
                            }
                            else if (Directory.Exists(InstallationManager.LocalToGlobalPath(ins.installationDir)))
                            {
                                Directory.Delete(InstallationManager.LocalToGlobalPath(ins.installationDir), true);
                                insManager.RemoveInstallation(ins);
                                CTools.WriteResult(true);
                            }
                            else
                            {
                                insManager.RemoveInstallation(ins);
                                CTools.WriteResult(true);
                                CTools.WriteError("Installation path does not exist \"" + InstallationManager.LocalToGlobalPath(ins.installationDir) + "\"", 1);
                            }
                        }
                        catch (Exception)
                        {
                            CTools.WriteResult(false);
                            CTools.WriteError("Could not remove \"" + InstallationManager.LocalToGlobalPath(ins.installationDir) + "\"\nMake sure Minecraft is not running during uninstall");
                            Environment.Exit(1);
                        }

                        if (ins.modloaderProfile != null)
                        {
                            if (!insManager.DeleteLauncherProfile(ins))
                                CTools.WriteError("Removing launcher profile failed.", 1);
                        }

                    }

                }
                else
                {
                    //remove mod
                    List<CustomMod> modSearchResult = listParams.modSearch != "" ? ins.customMods.FindAll((e) => (e.slug?.StartsWith(listParams.modSearch) ?? false) || (e.projectId?.StartsWith(listParams.modSearch) ?? false)) : ins.customMods;
                    CustomMod? mod = null;
                    if (modSearchResult.Count == 1)
                    {
                        mod = modSearchResult[0];
                    }
                    else if (modSearchResult.Count > 1)
                    {
                        int modRes = CTools.ListDialog("Choose custom mod", modSearchResult.Select((e) => e.name + " slug: " + (e.slug ?? "??") + " projectId: " + (e.projectId ?? "??")));
                        if (modRes < 0)
                        {
                            CTools.WriteError("User input is required!");
                            Environment.Exit(1);
                        }
                        mod = modSearchResult[modRes];
                    }
                    else
                    {
                        CTools.WriteLine("No mod matches your search query");
                    }

                    if (mod != null)
                    {
                        CTools.WriteLine("Removing: " + mod.name);
                        try
                        {
                            if (mod.files.Count > 0)
                            {
                                foreach (string file in mod.files)
                                {
                                    CTools.Write(file);
                                    File.Delete(InstallationManager.LocalToGlobalPath(ins.installationDir + "/" + file));
                                    CTools.WriteResult(true);
                                }
                            }
                            else
                                CTools.WriteError("Mod does not contain any files", 1);
                            ins.customMods.Remove(mod);
                        }
                        catch (Exception)
                        {
                            if (mod.files.Count > 0)
                                CTools.WriteResult(false);
                            CTools.WriteError("Deletion of at least one file failed! This will lead to inconsistent behaviour.\nPlease try again or delete them manually: ");

                            foreach (string file in mod.files)
                            {
                                CTools.WriteLine(file);
                            }
                            Environment.Exit(1);
                        }
                    }
                }
            }
            
            insManager.Save();
        }
    }
}