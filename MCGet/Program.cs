using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text.Json;
using System.Net.Http.Json;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Web;
using MCGet.Platforms;
using System.Reflection;
using ConsoleTools;

namespace MCGet
{
    public class Program
    {
        public enum COMMANDS { NONE = 0,  INSTALL, SEARCH, RESTORE };

        public static string archPath = "";
        public static string dir = "";
        public static string minecraftDir = "";
        public static string tempDir = "/temp/";
        public static string backupDir = "/backup/";
        public static string archiveDir = "/archives/";
        public static string api_user_agent = "mc-get/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1") + " (ThunderClapLP/mc-get)";
        public static Backup backup = new Backup("");

        //command line args
        public static bool cSilent = false;
        public static bool cFixMissing = false;
        public static COMMANDS command = COMMANDS.NONE;
        public static List<string> commandParams = new List<string>();

        public static JsonDocument? manifestDoc = null;
        static void Main(string[] args)
        {
            bool invalidArgs = true;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s":
                        cSilent = true;
                        break;
                    case "-f":
                    case "--fix-missing":
                        cFixMissing = true;
                        invalidArgs = false;
                        break;
                    case "-h":
                    case "--help":
                        CTools.WriteLine(@"
Usage: 
    {ExecutableName} (flags) <archivepath>
    {ExecutableName} (flags) <command> (parameters)

Flags:
    -h / --help         :  displays this help page
    -s                  :  performs a silent install. No user input needed
    -f / --fix-missing  :  retries to download failed mods
    -m <path>           :  specifies minecraft installation path
    -v / --version      :  displays the current version

Commands:
    install (<slug> | <id> | <name>):<mcversion>:<modloader>
        installs a mod / modpack

    search <query>
        searches for modrinth projects
    
    restore
        deletes modpack and restores old state
    
Examples:
    {ExecutableName} install sodium:1.19.3:fabric
    {ExecutableName} install fabulously-optimized      
    {ExecutableName} -s install fabulously-optimized
    {ExecutableName} Fabulously.Optimized-4.10.5.mrpack
    {ExecutableName} -r
".Replace("{ExecutableName}", Assembly.GetExecutingAssembly().GetName().Name));
                        Environment.Exit(0);
                        break;
                    case "-m":
                        if (i < args.Length - 1)
                        {
                            minecraftDir = args[i + 1];
                            i++;
                        }
                        break;
                    case "-v":
                    case "--version":
                        CTools.WriteLine(Assembly.GetExecutingAssembly().GetName().Name + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown");
                        Environment.Exit(0);
                        break;
                    case "install":
                        if (i < args.Length - 1)
                        {
                            command = COMMANDS.INSTALL;
                            commandParams.Clear();
                            commandParams.Add(args[i + 1]);
                            i++;
                        }
                        invalidArgs = false;
                        break;
                    case "search":
                        if (i < args.Length - 1)
                        {
                            invalidArgs = false;
                            command = COMMANDS.SEARCH;
                            commandParams.Clear();
                            commandParams.Add(args[i + 1]);
                        }
                        break;
                    case "restore":
                        command = COMMANDS.RESTORE;
                        commandParams.Clear();
                        invalidArgs = false;
                        break;
                    default:
                        if (args[i].ToLower().EndsWith(".zip") || args[i].ToLower().EndsWith(".mrpack"))
                        {
                            archPath = Path.GetFullPath(args[i]);
                            invalidArgs = false;
                        }
                        break;
                }
            }

            if (invalidArgs || args.Length <= 0)
            {
                CTools.WriteLine("Usage: " + Assembly.GetExecutingAssembly().GetName().Name + " (<archivepath> | install <slug>)\n --help for all arguments");
                if (OperatingSystem.IsWindows())
                {
                    CTools.Write("Press any key to exit");
                    Console.ReadKey();
                }
                Environment.Exit(0);
                return;
            }

            dir = AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
            if (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsMacOS())
            {
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/." + Assembly.GetExecutingAssembly().GetName().Name;
            }
            //dir = System.IO.Directory.GetCurrentDirectory();

            backup = new Backup(dir + backupDir);



            //prepare
            Prepare();

            //restore backup
            if (!cFixMissing)
            {
                Spinner spinner = new Spinner(CTools.CursorTop);
                spinner.Update();
                switch(command)
                {
                    case COMMANDS.INSTALL:
                        string installLoader = "";
                        string installGameVersion = "";
                        string installName = commandParams[0].Split(":")[0];
                        for (int j = 1; j < commandParams[0].Split(":").Length; j++)
                        {
                            if (commandParams[0].Split(":")[j].ToLower().First() >= 'a' && commandParams[0].Split(":")[j].ToLower().First() <= 'z')
                            {
                                installLoader = commandParams[0].Split(":")[j].ToLower();
                            }
                            else
                            {
                                installGameVersion = commandParams[0].Split(":")[j];
                            }
                        }
                        if (installName != "")
                        {
                            List<string>? urls = Modrinth.GetProject(installName, installGameVersion, installLoader, spinner);
                            //download archive
                            if (urls != null && urls[0] != "")
                            {
                                if (urls[0].Split("|")[0] == "modpack")
                                {
                                    //modpack
                                    urls[0] = urls[0].Split("|").Last(); //delete modloaders
                                    CTools.Write("Downloading manifest file");
                                    spinner.top = CTools.CursorTop;
                                    if (!Networking.DownloadFile(urls[0], dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0])), spinner))
                                    {
                                        CTools.WriteResult(false);
                                        Environment.Exit(0);
                                        return;
                                    }

                                    archPath = dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0]));
                                    CTools.WriteResult(true);
                                }
                                else if (urls[0].Split("|")[0] == "mod")
                                {
                                    //single mod
                                    CTools.WriteError("Single mod:", 0);
                                    CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(urls[0].Split("|").Last())));
                                    if (urls.Count > 1)
                                    {
                                        CTools.WriteLine("With dependencies:");
                                        for (int i = 1; i < urls.Count; i++)
                                        {
                                            CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(urls[i].Split("|").Last())));
                                        }
                                    }
                                    CTools.WriteError("Compatible modloaders: " + urls[0].Split("|")[1], 0);
                                    urls[0] = urls[0].Split("|").Last();
                                    if (!CTools.ConfirmDialog("Install single mod", true))
                                    {
                                        //user canceled
                                        Environment.Exit(0);
                                        return;
                                    }
                                    CTools.Write("Downloading single mod");
                                    spinner.top = CTools.CursorTop;
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        if (!Networking.DownloadFile(urls[i], dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), spinner))
                                        {
                                            CTools.WriteResult(false);
                                            Environment.Exit(0);
                                            return;
                                        }
                                    }

                                    CTools.WriteResult(true);

                                    //copy mod

                                    CTools.Write("Copy mod");
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        try
                                        {
                                            File.Copy(dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), minecraftDir + "/mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), true);
                                        }
                                        catch
                                        {
                                            CTools.WriteResult(false);
                                            Environment.Exit(0);
                                            return;
                                        }
                                    }

                                    CTools.WriteResult(true);
                                    Environment.Exit(0);
                                    return;

                                }
                                else
                                {
                                    //unknown
                                    CTools.WriteError("Unknown project type: " + urls[0].Split("|")[0]);
                                    Environment.Exit(1);
                                    return;
                                }
                            }
                            else
                            {
                                CTools.WriteError("No project with slug or id '" + installName + "' was found" + (installGameVersion != "" ? (" for version " + installGameVersion) : ""));
                                Environment.Exit(1);
                                return;
                            }
                        }
                        break;
                    case COMMANDS.SEARCH:
                        Modrinth.SearchForProjects(commandParams[0], spinner);
                        Environment.Exit(0);
                        break;
                    case COMMANDS.RESTORE:
                        RestoreBackup();
                        CTools.WriteLine();
                        CTools.WriteLine("Success!");
                        if (OperatingSystem.IsWindows())
                        {
                            CTools.Write(" Press any key to exit");
                            Console.ReadKey();
                        }
                        Environment.Exit(0);
                        break;
                }

                RestoreBackup();

                CTools.Write("Cleaning up");
                if (backup.Clean())
                    CTools.WriteResult(true);
                else
                    CTools.WriteResult(false);

                if (archPath != "")
                    CopyArchive();
            }
            else
            {
                if (Path.IsPathRooted(backup.log.archiveFile))
                {
                    archPath = backup.log.archiveFile; //absolute path for backwardscompatibility
                } else
                {
                    archPath = dir + archiveDir + backup.log.archiveFile;
                }
            }
            //backup.log.archiveFile = archPath;

            //extract
            ExtractArchive();

            //load manifest
            LoadManifest();

            //Get Platform
            Platform platform = new CurseForge();;
            if (archPath.EndsWith(".mrpack"))
            {
                platform = new Modrinth();
            }

            if (!platform.InstallDependencies())
            {
                //ConsoleTools.WriteError("")
                if (!CTools.ConfirmDialog("Modloader installation failed! Continue anyway?", false))
                {
                    RevertChanges();
                    Environment.Exit(1);
                }
            }

            //download mods
            platform.DownloadMods();

            //perform backup
            BackupModsFolder();

            //perform install
            platform.InstallMods();

            if (!cFixMissing)
                CopyOverrides();

            backup.Save();

            CTools.Write("Cleaning up");
            try
            {
                if (Directory.Exists(dir + tempDir))
                    System.IO.Directory.Delete(dir + tempDir, true);
            }
            catch (Exception)
            {
                CTools.WriteResult(false);
            }
            CTools.WriteResult(true);

            CTools.WriteLine();
            CTools.Write("Installation successful!");
            if (OperatingSystem.IsWindows())
            {
                CTools.Write(" Press any key to exit");
                Console.ReadKey();
            }
            else
            {
                CTools.WriteLine();
            }


        }

        static void Prepare()
        {
            //clear and create dirs
            try
            {
                if (Directory.Exists(dir + tempDir))
                    System.IO.Directory.Delete(dir + tempDir, true);
                System.IO.Directory.CreateDirectory(dir + tempDir + "mods/");
                System.IO.Directory.CreateDirectory(dir + tempDir + "archive/");
            }
            catch (Exception)
            {
                CTools.WriteError("Could not create temporary directories");
                RevertChanges();
            }

            //try backup minecraft path
            if (minecraftDir == "")
            {
                minecraftDir = backup.log.minecraftPath + "";
            }

            if (minecraftDir == "")
            {
                if (cFixMissing)
                {
                    CTools.WriteError("Minecraft directory not found!");
                    Environment.Exit(0);
                }
                if (System.OperatingSystem.IsWindows())
                {
                    if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft"))
                    {
                        minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft";
                    }
                }
                else if (System.OperatingSystem.IsLinux())
                {
                    minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.minecraft";
                }
                else if (System.OperatingSystem.IsMacOS())
                {
                    minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Application Support/minecraft";
                }
            }
            bool confirmed = false;
            while (!confirmed)
            {
                if (!Directory.Exists(minecraftDir) || !Directory.Exists(minecraftDir + "/versions"))
                {
                    CTools.WriteError("Minecraft directory not found!");

                    if (cSilent || cFixMissing)
                        Environment.Exit(0);

                    confirmed = false;
                }
                else
                {
                    minecraftDir = Path.GetFullPath(minecraftDir);
                    CTools.WriteLine("Minecraft Directory Found: " + minecraftDir);
                    if (!cFixMissing)
                        confirmed = CTools.ConfirmDialog("Use this Directory?", true);
                    else
                        confirmed = true;

                }
                if (!confirmed)
                {
                    CTools.Write("Enter Minecraft Path: ");
                    minecraftDir = Console.ReadLine() + "";
                    if (minecraftDir != null && minecraftDir != "")
                        minecraftDir = Path.GetFullPath(minecraftDir);
                }
            }

            if (backup.log.minecraftPath + "" != minecraftDir)
            {
                backup.Clean();
                //backup = new Backup(backup.path);
            }
            backup.SetMinecraftPath(minecraftDir + "");

        }

        //Copy archive to ensure --fix-missing works after the origional archive is deleted or moved by the user.
        static void CopyArchive()
        {
            try
            {
                if (!Directory.Exists(dir + archiveDir))
                    System.IO.Directory.CreateDirectory(dir + archiveDir);

                if (!archPath.Contains(dir + archiveDir))
                {
                    File.Copy(archPath, dir + archiveDir + Path.GetFileName(archPath), true);

                    archPath = dir + archiveDir + Path.GetFileName(archPath);
                }
                backup.log.archiveFile = Path.GetFileName(archPath);
            }
            catch (Exception)
            {
                backup.log.archiveFile = archPath; //use absolute path on failure
            }
        }

        static void ExtractArchive()
        {
            if (!File.Exists(archPath))
            {
                CTools.WriteError("Could not find archive '" + archPath + "'");
                RevertChanges();
            }

            CTools.Write("Extracting: " + Path.GetFileName(archPath));
            try
            {
                ZipFile.ExtractToDirectory(archPath, dir + tempDir + "archive/");
                CTools.WriteResult(true);
            }
            catch (Exception e)
            {
                CTools.WriteResult(false);
                //Console.WriteLine(e.Message);
                RevertChanges();
                return;
            }
        }

        static bool LoadManifest()
        {
            string manifestPath = "";
            if (File.Exists(dir + tempDir + "archive/manifest.json"))
            {
                manifestPath = dir + tempDir + "archive/manifest.json";
            } else if (File.Exists(dir + tempDir + "archive/modrinth.index.json"))
            {
                manifestPath = dir + tempDir + "archive/modrinth.index.json";
            } else
            {
                CTools.WriteError("Could find manifest file");
                RevertChanges();
                return false;
            }

            CTools.Write("Loading manifest.json");

            try
            {
                //manifestFile = JsonSerializer.Deserialize<ManifestClasses.ManifestFile>(File.ReadAllText(dir + temp_dir + "archive/manifest.json"));
                manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));

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

        public static bool DownloadJavaIfNotPresent()
        {
            if (!System.OperatingSystem.IsWindows())
            {
                return false;
            }

            if (File.Exists(dir + "/java/jdk-19/bin/java.exe"))
            {
                return true; //already downloaded
            }

            Process java = new Process();
            java.StartInfo.FileName = "java";
            java.StartInfo.Arguments = "--version";
            try
            {
                java.Start();
                java.WaitForExit();
            }
            catch (Exception)
            {
                CTools.Write("Downloading Java");
                Spinner spinner = new Spinner(CTools.CursorTop);
                if (Networking.DownloadFile("https://download.java.net/java/GA/jdk19/877d6127e982470ba2a7faa31cc93d04/36/GPL/openjdk-19_windows-x64_bin.zip", dir + tempDir + "java.zip", spinner))
                {
                    try
                    {
                        if (!Directory.Exists(dir + "java/"))
                        {
                            Directory.CreateDirectory(dir + "/java/");
                        }
                        ZipFile.ExtractToDirectory(dir + tempDir + "java.zip", dir + "/java/", true);
                    }
                    catch (Exception e)
                    {
                        CTools.WriteLine(e.Message);
                        CTools.WriteResult(false);
                        return false;
                    }
                    CTools.WriteResult(true);
                    return true;
                }
                CTools.WriteResult(false);
            }

            return false;
        }

        static void BackupModsFolder()
        {
            string modsDir = Path.GetFullPath(minecraftDir + "/mods");
            if (Directory.Exists(modsDir) || File.Exists(modsDir))
            {
                if (Directory.GetDirectories(modsDir).Length > 0 || Directory.GetFiles(modsDir).Length > 0)
                {
                    //backup mods
                    foreach (string mod in Directory.GetFiles(modsDir))
                    {
                        backup.BackopMod(mod, true);
                    }

                    if (!cFixMissing)
                    {
                        CTools.WriteError("Mods directory is not empty.", 1);
                        try
                        {
                            if (CTools.ConfirmDialog("Delete ALL existing mods?", true))
                            {
                                if (Directory.Exists(modsDir))
                                    Directory.Delete(modsDir, true);
                                if (File.Exists(modsDir))
                                    File.Delete(modsDir);

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
        }

        static void CopyOverrides()
        {
            if (Directory.Exists(dir + tempDir + "archive/overrides") && (Directory.GetDirectories(dir + tempDir + "archive/overrides").Length > 0 || Directory.GetFiles(dir + tempDir + "archive/overrides").Length > 0))
            {
                CTools.WriteError("The modpack suggests custom configfiles.", 0);
                if (!CTools.ConfirmDialog("Do you want to override these? (Recommended)", true))
                    return;

                CTools.Write("Configuring mods");
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                try
                {
                    CopyDir(dir + tempDir + "archive/overrides", minecraftDir, bar);
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

        static void CopyDir(string source, string dest, ProgressBar? bar = null)
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

                backup.BackopOverride(newPath, File.Exists(newPath));

                File.Copy(file, newPath, true);

                if (bar != null)
                {
                    bar.value++;
                    bar.Update();
                }
            }
        }

        static void RestoreBackup()
        {
            if (backup.log.installedMods == null || backup.log.overrides == null)
                return;

            if (backup.log.installedMods.Count > 0 || backup.log.overrides.Count > 0)
            {
                if (!CTools.ConfirmDialog("Restore previously saved backup?", true))
                    return;

                Spinner spinner = new Spinner(CTools.CursorTop);
                //spinner.minSpinnerTime = 150;
                spinner.top = CTools.CursorTop;
                CTools.Write("Restoring Backups");

                //delete automatically added launcher profile
                backup.DeleteLauncherProfile();

                backup.updateProgress += (int progress) =>
                {
                    spinner.Update();
                };

                if (!backup.RestoreMods() || !backup.RestoreOverrides())
                {
                    CTools.WriteResult(false);
                    CTools.WriteError("An error occured while restoring! Some files might be missing", 1);
                    if (command == COMMANDS.RESTORE || !CTools.ConfirmDialog("Continue anyway?", true))
                    {
                        System.Environment.Exit(0);
                        return;
                    }
                } else
                {
                    CTools.WriteResult(true);
                }

                backup.Clean();
            }


        }

        public static void RevertChanges()
        {
            CTools.WriteError("Failed to install");
            if (!cFixMissing)
            {
                RestoreBackup();
                CTools.Write("Cleaning up");
                if (backup.Clean())
                    CTools.WriteResult(true);
                else
                    CTools.WriteResult(false);
            }
            System.Environment.Exit(1);
        }

    }
}