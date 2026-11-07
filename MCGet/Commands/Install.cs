using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using ConsoleTools;
using MCGet.Platforms;

namespace MCGet.Commands
{
    public class InstallParams : CommandParams
    {
        public string installLoader { get; }
        public string installGameVersion { get; }
        public string installModVersion { get; }
        public string installName { get; }
        public string? extractedName { get; }
        public InstallParams(string[] parameters, string installLoader, string installGameVersion, string installModVersion, string installName) : base(parameters)
        {
            this.installLoader = installLoader;
            this.installGameVersion = installGameVersion;
            this.installModVersion = installModVersion;
            this.installName = installName;
        }
        public InstallParams(string[] parameters, string extractedName) : base(parameters)
        {
            this.installLoader = "";
            this.installGameVersion = "";
            this.installModVersion = "";
            this.installName = "";
            this.extractedName = extractedName;
        }
    }
    public class Install : Command<InstallParams>
    {
        public string dir;
        public string tempDir;
        public string backupDir;
        public string archiveDir;
        InstallationManager insManager;
        InstallLogger installLogger;
        public bool modifyExisting = false;
        public string extractedName = "";


        public Install(Flags flags, Spinner spinner, InstallationManager insManager, InstallLogger installLogger, string dir, string tempDir, string backupDir, string archiveDir) : base(flags, spinner)
        {
            this.insManager = insManager;
            this.installLogger = installLogger;
            this.dir = dir;
            this.tempDir = tempDir;
            this.backupDir = backupDir;
            this.archiveDir = archiveDir;
        }
        private GetProjectResult? GetProject(string installName, string installGameVersion, string installModVersion, string installLoader, Type? platformType, Spinner spinner)
        {
            GetProjectResult? result = null;
            Task<GetProjectResult>? mrResult = null;
            Task<GetProjectResult>? cfResult = null;
            if (platformType == null || platformType.Equals(typeof(Modrinth)))
                mrResult = Modrinth.GetProject(installName, installGameVersion, installModVersion, installLoader);
            if (platformType == null || platformType.Equals(typeof(CurseForge)))
                cfResult = CurseForge.GetProject(installName, installGameVersion, installModVersion, installLoader);
            spinner.top = CTools.CursorTop;
            spinner.msg = "Getting project info";
            spinner.StartAnimation();
            mrResult?.Wait();
            cfResult?.Wait();
            spinner.StopAnimation();

            //CTools.WriteResult(true);
            if ((mrResult?.Result.success ?? false) && (cfResult?.Result.success ?? false))
            {
                CTools.WriteResult(true, spinner);
                char choice = CTools.ChoiceDialog("Install from (M)odrinth or (C)urseForge?", new char[] { 'm', 'c' }, 'm');
                if (choice == 'm')
                {
                    result = mrResult.Result;
                }
                else
                {
                    result = cfResult.Result;
                }
            }
            else if (mrResult?.Result.success ?? false)
            {
                CTools.WriteResult(true, spinner);
                result = mrResult.Result;
            }
            else if (cfResult?.Result.success ?? false)
            {
                CTools.WriteResult(true, spinner);
                result = cfResult.Result;
            }
            else
            {
                CTools.WriteResult(false, spinner);
            }

            if (mrResult?.Result.error == GetProjectResult.ErrorCode.ConnectionFailed)
                CTools.WriteError("Connection to Modrinth failed", 1);
            else if (mrResult?.Result.error == GetProjectResult.ErrorCode.Gone)
                CTools.WriteError("The used Modrinth API version is no longer available! Make sure to use the newest version of this software");
            if (cfResult?.Result.error == GetProjectResult.ErrorCode.ConnectionFailed)
                CTools.WriteError("Connection to CurseForge failed", 1);
            else if (cfResult?.Result.error == GetProjectResult.ErrorCode.ConnectionRefused)
            {
                CTools.WriteError("Connection to CurseForge refused! Did you set the API up correctly?", 1);
                CTools.WriteLine("  cfApiUrl=" + insManager.installations.settings.Get(Settings.CF_API_URL));
                if (insManager.installations.settings.Get(Settings.CF_API_KEY) == "")
                {
                    CTools.WriteLine("  cfApiKey is not set!");
                    CTools.WriteError("You can set the API key with: " + Assembly.GetExecutingAssembly().GetName().Name + " --set cfApiKey=<your key>", 0);
                }
                else
                {
                    CTools.WriteLine("  cfApiKey is set!");
                }
            }

            if (result == null)
            {
                if (mrResult?.Result.error == GetProjectResult.ErrorCode.NotFound)
                    result = mrResult.Result;
                else if (cfResult?.Result.error == GetProjectResult.ErrorCode.NotFound)
                    result = cfResult.Result;
            }

            return result;
        }

        public void SetupInstallDir()
        {
            if (insManager.currInstallation.isServer)
            {
                if ((AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory()).Replace("\\", "/").TrimEnd('/') == InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir.Replace("\\", "/").TrimEnd('/')))
                {
                    CTools.WriteError("Can't install server directly in the " + Assembly.GetExecutingAssembly().GetName().Name + " directory!");
                    Environment.Exit(1);
                }
                Installation? ins = insManager.installations.installations.Find((e) => e.installationDir == insManager.currInstallation.installationDir);
                if (ins != null)
                {
                    modifyExisting = true;
                    insManager.currInstallation = ins;
                    CTools.WriteError("Upgrading Server", 0);
                }
                else if (Directory.Exists(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir.Replace("\\", "/"))))
                {
                    if (Directory.GetFileSystemEntries(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir.Replace("\\", "/"))).Length > 0)
                    {
                        CTools.WriteError("Installation directory is not empty. The remove command will not be able to delete the installed files!", 1);
                        if (!(flags.Silent || CTools.ConfirmDialog("Continue anyway?", false)))
                        {
                            Environment.Exit(1);
                        }
                        insManager.currInstallation.installationDirWasEmpty = false;
                    }
                }
            }

            if (insManager.currInstallation.installationDir == "")
            {
                if (CTools.ConfirmDialog("Install modpack into \"" + InstallationManager.LocalToGlobalPath(insManager.installations.settings.Get(Settings.DEFAULT_INSTALLATION_PATH)).Replace("\\", "/") + "\"?", true))
                {
                    insManager.currInstallation.installationDir = insManager.installations.settings.Get(Settings.DEFAULT_INSTALLATION_PATH);
                    try
                    {
                        //make sure install dir exists for disk space calculation to work on linux
                        Directory.CreateDirectory(InstallationManager.LocalToGlobalPath(insManager.installations.settings.Get(Settings.DEFAULT_INSTALLATION_PATH)));
                    }
                    catch (Exception) { }
                }
                else
                {
                    bool insDirValid = false;
                    while (!insDirValid)
                    {
                        CTools.Write("Enter target installation dir: ");
                        insManager.currInstallation.installationDir = Console.ReadLine() ?? "";
                        if (Directory.Exists(insManager.currInstallation.installationDir))
                            insDirValid = true;
                        else
                            CTools.WriteError("Directory does not exist");
                    }

                    //set default?
                    if (CTools.ConfirmDialog("Use selected dir for all future installations?", false))
                        insManager.installations.settings.Set(Settings.DEFAULT_INSTALLATION_PATH, insManager.currInstallation.installationDir);
                }
            }
            if (!modifyExisting && !insManager.currInstallation.isServer)
                insManager.currInstallation.installationDir = insManager.currInstallation.installationDir.Replace("\\", "/").TrimEnd('/') + "/" + insManager.currInstallation.slug + insManager.currInstallation.Id;

        }

        private void RestoreBackup()
        {
            if (installLogger.log.installedMods == null || installLogger.log.overrides == null)
                return;

            if (installLogger.log.installedMods.Count > 0 || installLogger.log.overrides.Count > 0)
            {
                if (!CTools.ConfirmDialog("Restore previously saved backup?", true))
                    return;

                Spinner spinner = new Spinner(CTools.CursorTop);
                spinner.top = CTools.CursorTop;
                CTools.Write("Restoring Backups");

                //delete automatically added launcher profile
                if (!modifyExisting)
                    installLogger.DeleteLauncherProfile();

                installLogger.updateProgress += (int progress) =>
                {
                    spinner.Update();
                };

                if (!installLogger.RestoreMods() || !installLogger.RestoreOverrides())
                {
                    CTools.WriteResult(false);
                    CTools.WriteError("An error occured while restoring! Some files might be missing", 1);
                    if (!CTools.ConfirmDialog("Continue anyway?", true))
                    {
                        System.Environment.Exit(0);
                        return;
                    }
                } else
                {
                    CTools.WriteResult(true);
                }

                installLogger.Clean();
            }


        }

        private void RevertChanges()
        {
            CTools.WriteError("Failed to install");
            if (modifyExisting)
                RestoreBackup();
            else
            {
                Spinner spinner = new Spinner("Reverting changes", CTools.CursorTop);
                spinner.top = CTools.CursorTop;
                spinner.StartAnimation();
                insManager.DeleteLauncherProfile(insManager.currInstallation);
                try
                {
                    Directory.Delete(insManager.currInstallation.installationDir, true);
                    spinner.StopAnimation();
                    CTools.WriteResult(true, spinner);
                }
                catch (Exception)
                {
                    spinner.StopAnimation();
                    CTools.WriteResult(false, spinner);
                }
            }
            CTools.Write("Cleaning up");
            if (installLogger.Clean())
                CTools.WriteResult(true);
            else
                CTools.WriteResult(false);
            System.Environment.Exit(1);
        }

        private void ExtractArchive()
        {
            if (!File.Exists(insManager.currInstallation.archivePath))
            {
                CTools.WriteError("Could not find archive '" + insManager.currInstallation.archivePath + "'");
                RevertChanges();
            }

            Spinner spinner = new Spinner("Extracting: " + Path.GetFileName(insManager.currInstallation.archivePath), CTools.CursorTop);
            spinner.Draw();
            try
            {
                ZipFile.ExtractToDirectory(insManager.currInstallation.archivePath, dir + tempDir + "archive/");
                CTools.WriteResult(true, spinner);
            }
            catch (Exception)
            {
                CTools.WriteResult(false, spinner);
                RevertChanges();
                return;
            }
        }

        private bool LoadManifest()
        {
            string manifestPath;
            if (File.Exists(dir + tempDir + "archive/manifest.json"))
            {
                manifestPath = dir + tempDir + "archive/manifest.json";
            } else if (File.Exists(dir + tempDir + "archive/modrinth.index.json"))
            {
                manifestPath = dir + tempDir + "archive/modrinth.index.json";
            } else
            {
                CTools.WriteError("Could not find manifest file");
                RevertChanges();
                return false;
            }

            CTools.Write("Loading manifest.json");

            try
            {
                Program.manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            }
            catch (Exception)
            {
                CTools.WriteResult(false);
                RevertChanges();
                return false;
            }
            CTools.WriteResult(true);

            return true;
        }

        private void BackupModsFolder()
        {
            string modsDir = InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir + "/mods");
            if (Directory.Exists(modsDir) || File.Exists(modsDir))
            {
                if (Directory.GetDirectories(modsDir).Length > 0 || Directory.GetFiles(modsDir).Length > 0)
                {
                    //backup mods
                    Spinner spinner = new Spinner("Backing up mods directory", CTools.CursorTop);
                    spinner.StartAnimation();
                    bool backupFailed = false;
                    foreach (string mod in Directory.GetFiles(modsDir))
                    {
                        if (!installLogger.BackopMod(mod, true))
                        {
                            backupFailed = true;
                        }
                    }
                    spinner.StopAnimation();

                    if (backupFailed)
                    {
                        CTools.WriteResult(false, spinner);
                        CTools.WriteError("Not all previously installed mods could be backed up", 1);
                    }
                    else
                    {
                        CTools.WriteResult(true, spinner);
                    }

                    CTools.WriteError("Mods directory is not empty.", 1);
                    try
                    {
                        if (CTools.ConfirmDialog("Delete ALL existing mods? (custom mods will be lost)", true))
                        {
                            if (Directory.Exists(modsDir))
                                Directory.Delete(modsDir, true);
                            if (File.Exists(modsDir))
                                File.Delete(modsDir);
                            //TODO: Properly delete all files from the mod. not only in the mods folder
                            insManager.currInstallation.customMods = insManager.currInstallation.customMods.FindAll((e) => !e.files.Any((f) => f.StartsWith("/mods/")));
                        }
                    }
                    catch (Exception)
                    {
                        CTools.WriteError("Deletion failed");
                        RevertChanges();
                        return;
                    }
                }
            }
        }

        //Copy archive to ensure --fix-missing works after the original archive is deleted or moved by the user.
        //TODO: decide if this is really needed. --fix-missing does not exist anymore
        private void CopyArchive()
        {
            try
            {
                if (!Directory.Exists(dir + archiveDir))
                    System.IO.Directory.CreateDirectory(dir + archiveDir);

                if (!insManager.currInstallation.archivePath.Contains(dir + archiveDir))
                {
                    File.Copy(insManager.currInstallation.archivePath, dir + archiveDir + Path.GetFileName(insManager.currInstallation.archivePath), true);

                    insManager.currInstallation.archivePath = dir + archiveDir + Path.GetFileName(insManager.currInstallation.archivePath);
                }
                installLogger.log.archiveFile = Path.GetFileName(insManager.currInstallation.archivePath);
            }
            catch (Exception)
            {
                installLogger.log.archiveFile = insManager.currInstallation.archivePath; //use absolute path on failure
            }
        }

        private void CopyOverrides()
        {
            if (Directory.Exists(dir + tempDir + "archive/overrides") && (Directory.GetDirectories(dir + tempDir + "archive/overrides").Length > 0 || Directory.GetFiles(dir + tempDir + "archive/overrides").Length > 0))
            {
                CTools.WriteError("The modpack suggests custom config files.", 0);
                if (!CTools.ConfirmDialog("Do you want to override these? (Recommended)", true))
                    return;

                CTools.Write("Configuring mods");
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                try
                {
                    CopyDir(dir + tempDir + "archive/overrides", InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir), bar);
                }
                catch (Exception)
                {
                    bar.Clear();
                    CTools.WriteResult(false);
                    RevertChanges();
                }
                bar.Clear();
                CTools.WriteResult(true);
            }
        }

        private void CopyDir(string source, string dest, ProgressBar? bar = null)
        {
            source = Path.GetFullPath(source);
            foreach (string dir in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
            {
                string newPath = dest + dir.Substring(source.Length);
                if (!Directory.Exists(newPath) && !File.Exists(newPath))
                {
                    Directory.CreateDirectory(newPath);
                }
            }

            string[] files = Directory.GetFiles(source, "*", SearchOption.AllDirectories);

            if (bar != null)
            {
                bar.value = 0;
                bar.max = files.Length;
                bar.Update();
            }

            foreach (string file in files)
            {
                string newPath = dest + file.Substring(source.Length);

                installLogger.BackopOverride(newPath, File.Exists(newPath));

                File.Copy(file, newPath, true);

                if (bar != null)
                {
                    bar.value++;
                    bar.Update();
                }
            }
        }

        private void DownloadModpack(GetProjectResult result)
        {
            //check for existing installations
            List<Installation> existingInstallations = insManager.GetInstallationsBySlug(insManager.currInstallation.slug);
            if (insManager.currInstallation.installationDir != "") //filter by --path
                existingInstallations = existingInstallations.FindAll((e) => InstallationManager.LocalToGlobalPath(e.installationDir).Replace("\\", "/").StartsWith(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir).Replace("\\", "/")));
            if (existingInstallations.Count > 0)
            {
                if (!insManager.currInstallation.isServer && CTools.ConfirmDialog("Upgrade existing installation?", true))
                {
                    modifyExisting = true;
                    if (existingInstallations.Count == 1)
                    {
                        insManager.currInstallation = existingInstallations[0];
                    }
                    else
                    {
                        ProfileHandler ph = new ProfileHandler();
                        string profilePath = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                        if (profilePath != "")
                            ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                        CTools.WriteLine("    " + CTools.LimitText("ID", int.MaxValue.ToString().Length, true) + " | ProfileName");
                        int insRes = CTools.ListDialog("Choose installation",
                            existingInstallations.Select((e) => {
                                if (profilePath != e.minecraftDir)
                                {
                                    profilePath = e.minecraftDir;
                                    ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                                }
                                return CTools.LimitText(e.Id ?? "??", int.MaxValue.ToString().Length, true) + " | " + (ph.GetProfileName(e.modloaderProfile ?? "") ?? "??");
                            })); if (insRes < 0)
                        {
                            CTools.WriteError("User input is required!");
                            Environment.Exit(1);
                        }
                        insManager.currInstallation = existingInstallations[insRes];
                    }
                }
            }
            
            SetupInstallDir();

            spinner.top = CTools.CursorTop;
            spinner.msg = "Downloading pack file";
            if (!Networking.DownloadFile(result.urls[0], dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(result.urls[0])), spinner))
            {
                CTools.WriteResult(false, spinner);
                Environment.Exit(0);
                return;
            }

            insManager.currInstallation.archivePath = dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(result.urls[0]));
            CTools.WriteResult(true, spinner);
        }

        private void InstallSingleResource(GetProjectResult result, InstallParams installParams)
        {
            string resourceTypeName = "";
            string resourceTargetFolderName = "";
            string modloader = "";

            switch(result.projectType)
            {
                case ProjectType.Mod:
                    resourceTypeName = "mod";
                    resourceTargetFolderName = "mods";
                    break;
                case ProjectType.Shader:
                    resourceTypeName = "shader";
                    resourceTargetFolderName = "shaderpacks";
                    break;
                case ProjectType.ResourcePack:
                    resourceTypeName = "resource pack";
                    resourceTargetFolderName = "resourcepacks";
                    break;
            }
            CTools.WriteError($"Single {resourceTypeName}: {result.name}", 0);
            //check for existing installations
            List<Installation> existingInstallations = insManager.installations.installations;
            if (insManager.currInstallation.installationDir != "") //filter by --path
                existingInstallations = existingInstallations.FindAll((e) => InstallationManager.LocalToGlobalPath(e.installationDir).Replace("\\", "/").StartsWith(InstallationManager.LocalToGlobalPath(insManager.currInstallation.installationDir).Replace("\\", "/")));
            Installation? ins = null;
            if (existingInstallations.Count > 0)
            {
                modifyExisting = true;
                if (existingInstallations.Count == 1)
                {
                    ins = existingInstallations[0];
                }
                else
                {
                    ProfileHandler ph = new ProfileHandler();
                    string profilePath = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                    if (profilePath != "")
                        ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                    CTools.WriteLine("    " + CTools.LimitText("ID", int.MaxValue.ToString().Length, true) + " | ProfileName | slug");
                    int insRes = CTools.ListDialog("Choose installation",
                        existingInstallations.Select((e) => {
                            if (profilePath != e.minecraftDir)
                            {
                                profilePath = e.minecraftDir;
                                ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                            }
                            return CTools.LimitText(e.Id ?? "??", int.MaxValue.ToString().Length, true) + " | " + (ph.GetProfileName(e.modloaderProfile ?? "") ?? "??") + " | " + (e.slug ?? "??");
                        })); if (insRes < 0)
                    {
                        CTools.WriteError("User input is required!");
                        Environment.Exit(1);
                    }
                    ins = existingInstallations[insRes];
                }
            }

            if (ins == null)
            {
                CTools.WriteLine($"No existing project to install {resourceTypeName} into");
                Environment.Exit(1);
            }

            //get project again with mc version and loader from installation
            //TODO: decide if install pack/mod would be better to avoid double search
            if (result.projectType == ProjectType.Mod)
                modloader = ins.modloader?.Split("-")[0] ?? installParams.installLoader;
            GetProjectResult? exactResult = GetProject(installParams.installName, ins.mcVersion ?? installParams.installGameVersion, installParams.installModVersion, modloader, result.platformType, spinner);
            if (exactResult == null || exactResult.urls.Count == 0 || exactResult.urls[0] == "")
            {
                CTools.WriteLine($"No compatible version of the {resourceTypeName} found for this installation");
                Environment.Exit(1);
            }


            CTools.WriteLine("Install");
            CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[0])));
            if (exactResult.urls.Count > 1)
            {
                CTools.WriteLine("with dependencies:");
                for (int i = 1; i < exactResult.urls.Count; i++)
                {
                    CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[i])));
                }
            }
            if (exactResult.loader != "")
                CTools.WriteError("Compatible modloaders: " + exactResult.loader, 0);
            if (!CTools.ConfirmDialog($"Install single {resourceTypeName}", true))
            {
                //user canceled
                Environment.Exit(0);
                return;
            }
            spinner.top = CTools.CursorTop;
            spinner.msg = $"Downloading single {resourceTypeName}";
            for (int i = 0; i < exactResult.urls.Count; i++)
            {
                if (!Networking.DownloadFile(exactResult.urls[i], dir + tempDir + resourceTargetFolderName + "/" + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[i])), spinner))
                {
                    CTools.WriteResult(false, spinner);
                    Environment.Exit(0);
                    return;
                }
            }

            CTools.WriteResult(true, spinner);

            CustomMod custMod = new CustomMod();
            custMod.slug = insManager.currInstallation.slug;
            custMod.name = extractedName;
            //copy resource

            CTools.Write($"Copy {resourceTypeName}");
            for (int i = 0; i < exactResult.urls.Count; i++)
            {
                try
                {
                    File.Copy(dir + tempDir + resourceTargetFolderName + "/" + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[i])), InstallationManager.LocalToGlobalPath(ins.installationDir) + "/" + resourceTargetFolderName + "/" + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[i])), true);
                    custMod.files.Add("/" + resourceTargetFolderName + "/" + Path.GetFileName(HttpUtility.UrlDecode(exactResult.urls[i])));
                }
                catch
                {
                    CTools.WriteResult(false);
                    Environment.Exit(0);
                    return;
                }
            }
            ins.customMods.Add(custMod);
            insManager.Save();

            CTools.WriteResult(true);
            Environment.Exit(0);
            return;
        }
        public override void Run(InstallParams installParams)
        {
            if (installParams.installName != "")
            {

                GetProjectResult? result = GetProject(installParams.installName, installParams.installGameVersion, installParams.installModVersion, installParams.installLoader,
                    (!flags.CurseForge && !flags.Modrinth) ? null : (flags.Modrinth ? typeof(Modrinth) : typeof(CurseForge)), spinner);
                insManager.EnsureUniqueId(insManager.currInstallation);

                //download archive
                if (result != null && result.urls.Count > 0 && result.urls[0] != "")
                {
                    extractedName = result.name;
                    insManager.currInstallation.slug = result.slug;
                    if (result.projectType == ProjectType.Modpack)
                    {
                        //modpack
                        DownloadModpack(result);
                    }
                    else if (result.projectType == ProjectType.Mod || result.projectType == ProjectType.Shader || result.projectType == ProjectType.ResourcePack)
                    {
                        //single resource
                        InstallSingleResource(result, installParams);
                    }
                    else
                    {
                        //unknown
                        CTools.WriteError("Unknown project type");
                        Environment.Exit(1);
                        return;
                    }
                }
                else
                {
                    if (result?.error == GetProjectResult.ErrorCode.NotFound)
                        CTools.WriteError("No project with slug or id '" + installParams.installName + "' was found" + (installParams.installGameVersion != "" ? (" for version " + installParams.installGameVersion) : ""));
                    Environment.Exit(1);
                    return;
                }
            }
            else if (installParams.extractedName != null)
            {
                extractedName = installParams.extractedName;
                SetupInstallDir();
            }

            //Install archive
            CTools.Write("Cleaning up");
            if (installLogger.Clean())
                CTools.WriteResult(true);
            else
                CTools.WriteResult(false);

            if (insManager.currInstallation.archivePath != "")
                CopyArchive();

            //extract
            ExtractArchive();

            //load manifest
            if (!insManager.currInstallation.isServer || insManager.currInstallation.archivePath.EndsWith(".mrpack"))
                LoadManifest();
            else
            {
                //perform backup and delete existing mods in modfolder
                BackupModsFolder();
                //curseforge server install
                CTools.Write("Copy server files");
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                try
                {
                    string outputDir = dir + tempDir + "archive";
                    if (Directory.GetDirectories(outputDir).Length == 1 && Directory.GetFiles(outputDir).Length == 0)
                    {
                        outputDir = Directory.GetDirectories(outputDir)[0];
                    }
                    CopyDir(outputDir, insManager.currInstallation.installationDir, bar);
                }
                catch (Exception)
                {
                    bar.Clear();
                    CTools.WriteResult(false);
                    RevertChanges();
                }
                bar.Clear();
                CTools.WriteResult(true);
                //TODO: organize this better! Can't get mc version and modloader this way. Extract from CurseForge?
                insManager.currInstallation.modpackName = extractedName;
                insManager.AddInstallation(insManager.currInstallation);
                insManager.Save();
                Environment.Exit(0);
            }

            //Get Platform
            Platform platform = new CurseForge();;
            if (insManager.currInstallation.archivePath.EndsWith(".mrpack"))
            {
                platform = new Modrinth();
                platform.name = Program.manifestDoc?.RootElement.GetOrNull("name").ToString() ?? "";
                insManager.currInstallation.modpackName = platform.name;
            }

            if (extractedName != "" && platform.name == "")
            {
                platform.name = extractedName;
                insManager.currInstallation.modpackName = extractedName;
            }

            if (!platform.InstallDependencies(modifyExisting))
            {
                //ConsoleTools.WriteError("")
                if (!CTools.ConfirmDialog("Modloader installation failed! Continue anyway?", false))
                {
                    RevertChanges();
                    Environment.Exit(1);
                }
            }

            //download mods
            if (!platform.DownloadMods())
                RevertChanges();

            //perform backup
            BackupModsFolder();

            //perform install
            if (!platform.InstallMods())
                RevertChanges();

            CopyOverrides();

            //if (!cServer) //do not backup on server
            //do not save backup at all
            //installLogger.Save();
            insManager.AddInstallation(insManager.currInstallation);
            insManager.Save();

            CTools.Write("Cleaning up");
            try
            {
                bool backup_clean_success = installLogger.Clean();
                if (Directory.Exists(dir + tempDir))
                    System.IO.Directory.Delete(dir + tempDir, true);
                if (!backup_clean_success)
                    throw new Exception();
            }
            catch (Exception)
            {
                CTools.WriteResult(false);
            }
            CTools.WriteResult(true);

            {
                ProfileHandler ph = new ProfileHandler();
                string profilePath = insManager.currInstallation.minecraftDir;
                if (profilePath != "")
                    ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                string? profileName = ph.GetProfileName(insManager.currInstallation.modloaderProfile ?? "");
                if (profileName != null)
                    CTools.WriteError("MC profile name: " + profileName, 0);
            }

            CTools.WriteLine();
            CTools.Write("Installation successful!");
            if (OperatingSystem.IsWindows())
            {
                CTools.Write(" Press any key to exit");
                try { Console.ReadKey(); }
                catch {}
            }
            else
            {
                CTools.WriteLine();
            }
            return;
        }
    }
}