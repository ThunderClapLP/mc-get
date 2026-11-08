using System;
using System.Collections.Generic;
using System.Linq;
using ConsoleTools;

namespace MCGet.Commands
{
    public class ListParams : CommandParams
    {
        public enum Type
        {
            Invalid,
            Installs,
            Mods
        }

        public Type type { get; }
        public string search { get; }
        public ListParams(string[] parameters, Type type, string? search) : base(parameters)
        {
            this.type = type;
            this.search = search ?? "";
        }
    }
    public class List : Command<ListParams>
    {
        public List(Flags flags, Spinner spinner, InstallationManager insManager) : base(flags, spinner, insManager) {}

        public override void Run(ListParams listParams)
        {
            if (listParams.type == ListParams.Type.Installs)
            {
                //list installations
                ProfileHandler ph = new ProfileHandler();
                string profilePath = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                if (profilePath != "")
                    ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                List<Installation> installs = insManager.installations.installations;
                if (insManager.currInstallation.installationDir != "") //filter by --path
                    installs = installs.FindAll((e) => InstallationManager.LocalToGlobalPath(e.installationDir).Replace("\\", "/").StartsWith(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir).Replace("\\", "/")));
                foreach (Installation install in installs)
                {
                    if (profilePath != install.minecraftDir)
                    {
                        profilePath = install.minecraftDir;
                        ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                    }
                    CTools.WriteLine(install.modpackName + "\n  slug: " + (install.slug ?? "??") + "\n  Id: " + install.Id + "\n  ProfileName: " + (ph.GetProfileName(install.modloaderProfile ?? "") ?? "??") + "\n  mc version: " + (install.mcVersion ?? "??") + "\n  path: " + install.installationDir);

                }
            }
            else if (listParams.type == ListParams.Type.Mods)
            {
                //list mods
                List<Installation> searchResult = listParams.search != "" ? insManager.SearchInstallations(listParams.search, true) : insManager.installations.installations;
                if (insManager.currInstallation.installationDir != "") //filter by --path
                    searchResult = searchResult.FindAll((e) => InstallationManager.LocalToGlobalPath(e.installationDir).Replace("\\", "/").StartsWith(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir).Replace("\\", "/")));
                Installation? ins = null;
                if (searchResult.Count == 1)
                {
                    ins = searchResult[0];
                }
                else if (searchResult.Count > 1)
                {
                    ProfileHandler ph = new ProfileHandler();
                    string profilePath = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                    if (profilePath != "")
                        ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                    CTools.WriteLine("    " + CTools.LimitText("ID", int.MaxValue.ToString().Length, true) + " | ProfileName | slug");
                    int insRes = CTools.ListDialog("Choose installation to list custom mods of",
                        searchResult.Select((e) => {
                            if (profilePath != e.minecraftDir)
                            {
                                profilePath = e.minecraftDir;
                                ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                            }
                            return CTools.LimitText(e.Id ?? "??", int.MaxValue.ToString().Length, true) + " | " + (ph.GetProfileName(e.modloaderProfile ?? "") ?? "??") + " | " + (e.slug ?? "??");
                            }));
                    if (insRes < 0)
                    {
                        CTools.WriteError("User input is required!");
                        Environment.Exit(1);
                    }
                    ins = searchResult[insRes];
                }
                else
                {
                    CTools.WriteLine("No installation matches your search query");
                }

                if (ins != null)
                {
                    CTools.WriteLine("Custom mods for " + ins.modpackName + " (" + ins.Id + "):");
                    if (ins.customMods.Count == 0)
                        CTools.WriteLine("  None");
                    else
                    {
                        foreach (CustomMod mod in ins.customMods)
                        {
                            CTools.WriteLine("  " + mod.name + " | slug: " + (mod.slug ?? "??") + " Id: " + (mod.projectId ?? "??"));
                            if (mod.files != null)
                            {
                                foreach (string file in mod.files)
                                {
                                    CTools.WriteLine("    " + file);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}