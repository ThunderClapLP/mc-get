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

namespace MCGet
{
    public class Program
    {
        public enum COMMANDS { NONE = 0,  INSTALL, SEARCH };

        public static string archPath = "";
        public static string dir = "";
        public static string minecraftDir = "";
        public static string tempDir = "/temp/";
        public static string backupDir = "/backup/";
        public static string archiveDir = "/archives/";
        public static string api_user_agent = "mc-get/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1") + " (ThunderClapLP/mc-get)";
        public static Backup backup = new Backup("");

        //command line args
        public static bool cRestore = false;
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
                    case "-r":
                        cRestore = true;
                        invalidArgs = false;
                        break;
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
                        Console.WriteLine(@"
Usage: 
    {ExecutableName} (flags) <archivepath>
    {ExecutableName} (flags) <command> (parameters)

Flags:
    -h / --help         :  displays this help page
    -r                  :  deletes modpack and restores old state
    -s                  :  performs a silent install. No user input needed
    -f / --fix-missing  :  retries to download failed mods
    -m <path>           :  specifies minecraft installation path
    -v / --version      :  displays the current version

Commands:
    install (<slug> | <id> | <name>):<mcversion>:<modloader>
        installs a mod / modpack

    search <query>
        searches for modrinth projects
    
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
                        Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Name + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown");
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
                Console.WriteLine("Usage: " + Assembly.GetExecutingAssembly().GetName().Name + " (<archivepath> | install <slug>)\n --help for all arguments");
                if (OperatingSystem.IsWindows())
                {
                    Console.Write("Press any key to exit");
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
                if (cRestore)
                {
                    RestoreBackup();
                    Console.WriteLine();
                    Console.WriteLine("Success!");
                    if (OperatingSystem.IsWindows())
                    {
                        Console.Write(" Press any key to exit");
                        Console.ReadKey();
                    }
                    Environment.Exit(0);
                    return;
                }

                Spinner spinner = new Spinner(Console.CursorTop);
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
                                    Console.Write("Downloading manifest file");
                                    spinner.top = Console.CursorTop;
                                    if (!Networking.DownloadFile(urls[0], dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0])), spinner))
                                    {
                                        ConsoleTools.WriteResult(false);
                                        Environment.Exit(0);
                                        return;
                                    }

                                    archPath = dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0]));
                                    ConsoleTools.WriteResult(true);
                                }
                                else if (urls[0].Split("|")[0] == "mod")
                                {
                                    //single mod
                                    ConsoleTools.WriteError("Single mod:", 0);
                                    Console.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(urls[0].Split("|").Last())));
                                    if (urls.Count > 1)
                                    {
                                        Console.WriteLine("With dependencies:");
                                        for (int i = 1; i < urls.Count; i++)
                                        {
                                            Console.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(urls[i].Split("|").Last())));
                                        }
                                    }
                                    ConsoleTools.WriteError("Compatible modloaders: " + urls[0].Split("|")[1], 0);
                                    urls[0] = urls[0].Split("|").Last();
                                    if (!ConsoleTools.ConfirmDialog("Install single mod", true))
                                    {
                                        //user canceled
                                        Environment.Exit(0);
                                        return;
                                    }
                                    Console.Write("Downloading single mod");
                                    spinner.top = Console.CursorTop;
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        if (!Networking.DownloadFile(urls[i], dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), spinner))
                                        {
                                            ConsoleTools.WriteResult(false);
                                            Environment.Exit(0);
                                            return;
                                        }
                                    }

                                    ConsoleTools.WriteResult(true);

                                    //copy mod

                                    Console.Write("Copy mod");
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        try
                                        {
                                            File.Copy(dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), minecraftDir + "/mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), true);
                                        }
                                        catch
                                        {
                                            ConsoleTools.WriteResult(false);
                                            Environment.Exit(0);
                                            return;
                                        }
                                    }

                                    ConsoleTools.WriteResult(true);
                                    Environment.Exit(0);
                                    return;

                                }
                                else
                                {
                                    //unknown
                                    ConsoleTools.WriteError("Unknown project type: " + urls[0].Split("|")[0]);
                                    Environment.Exit(0);
                                    return;
                                }
                            }
                            else
                            {
                                ConsoleTools.WriteError("No project with slug or id '" + installName + "' was found" + (installGameVersion != "" ? (" for version " + installGameVersion) : ""));
                                Environment.Exit(0);
                                return;
                            }
                        }
                        break;
                    case COMMANDS.SEARCH:
                        Modrinth.SearchForProjects(commandParams[0], spinner);
                        Environment.Exit(0);
                        break;
                }

                RestoreBackup();

                Console.Write("Cleaning up");
                if (backup.Clean())
                    ConsoleTools.WriteResult(true);
                else
                    ConsoleTools.WriteResult(false);

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

            platform.InstallDependencies();

            //download mods
            platform.DownloadMods();

            //perform backup
            BackupModsFolder();

            //perform install
            platform.InstallMods();

            if (!cFixMissing)
                CopyOverrides();

            backup.Save();

            Console.Write("Cleaning up");
            try
            {
                if (Directory.Exists(dir + tempDir))
                    System.IO.Directory.Delete(dir + tempDir, true);
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
            }
            ConsoleTools.WriteResult(true);

            Console.WriteLine();
            Console.Write("Installation successful!");
            if (OperatingSystem.IsWindows())
            {
                Console.Write(" Press any key to exit");
                Console.ReadKey();
            }
            else
            {
                Console.WriteLine();
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
                ConsoleTools.WriteError("Could not create temporary directories");
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
                    ConsoleTools.WriteError("Minecraft directory not found!");
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
                    ConsoleTools.WriteError("Minecraft directory not found!");

                    if (cSilent || cFixMissing)
                        Environment.Exit(0);

                    confirmed = false;
                }
                else
                {
                    minecraftDir = Path.GetFullPath(minecraftDir);
                    Console.WriteLine("Minecraft Directory Found: " + minecraftDir);
                    if (!cFixMissing)
                        confirmed = ConsoleTools.ConfirmDialog("Use this Directory?", true);
                    else
                        confirmed = true;

                }
                if (!confirmed)
                {
                    Console.Write("Enter Minecraft Path: ");
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

            }
        }

        static void ExtractArchive()
        {
            Console.Write("Extracting: " + Path.GetFileName(archPath));
            try
            {
                ZipFile.ExtractToDirectory(archPath, dir + tempDir + "archive/");
                ConsoleTools.WriteResult(true);
            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
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
                ConsoleTools.WriteError("Could find manifest file");
                RevertChanges();
                return false;
            }

            Console.Write("Loading manifest.json");

            try
            {
                //manifestFile = JsonSerializer.Deserialize<ManifestClasses.ManifestFile>(File.ReadAllText(dir + temp_dir + "archive/manifest.json"));
                manifestDoc = JsonDocument.Parse(File.ReadAllText(manifestPath));

            }
            catch (Exception)
            {
                ConsoleTools.WriteResult(false);
                RevertChanges();
                return false;
            }
            ConsoleTools.WriteResult(true);

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
                Console.Write("Downloading Java");
                Spinner spinner = new Spinner(Console.CursorTop);
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
                        Console.WriteLine(e.Message);
                        ConsoleTools.WriteResult(false);
                        return false;
                    }
                    ConsoleTools.WriteResult(true);
                    return true;
                }
                ConsoleTools.WriteResult(false);
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
                        ConsoleTools.WriteError("Mods directory is not empty.", 1);
                        try
                        {
                            if (ConsoleTools.ConfirmDialog("Delete ALL existing mods?", true))
                            {
                                if (Directory.Exists(modsDir))
                                    Directory.Delete(modsDir, true);
                                if (File.Exists(modsDir))
                                    File.Delete(modsDir);

                            }
                        }
                        catch (Exception)
                        {
                            ConsoleTools.WriteError("Deletion failed");
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
                ConsoleTools.WriteError("The modpack suggests custom configfiles.", 0);
                if (!ConsoleTools.ConfirmDialog("Do you want to override these? (Recommended)", true))
                    return;

                Console.Write("Configuring mods");
                ProgressBar bar = new ProgressBar(0, ConsoleTools.DockRight());
                bar.fill = true;
                try
                {
                    CopyDir(dir + tempDir + "archive/overrides", minecraftDir, bar);
                }
                catch (Exception)
                {
                    bar.Clear();
                    ConsoleTools.WriteResult(false);
                    RevertChanges();
                }
                bar.Clear();
                ConsoleTools.WriteResult(true);
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
                if (!ConsoleTools.ConfirmDialog("Restore previously saved backup?", true))
                    return;

                Spinner spinner = new Spinner(Console.CursorTop);
                //spinner.minSpinnerTime = 150;
                spinner.top = Console.CursorTop;
                Console.Write("Restoring Backups");


                backup.updateProgress += (int progress) =>
                {
                    spinner.Update();
                };

                if (!backup.RestoreMods() || !backup.RestoreOverrides())
                {
                    ConsoleTools.WriteResult(false);
                    ConsoleTools.WriteError("An error occured while restoring! Some files might be missing", 1);
                    if (cRestore || !ConsoleTools.ConfirmDialog("Continue anyway?", true))
                    {
                        System.Environment.Exit(0);
                        return;
                    }
                } else
                {
                    ConsoleTools.WriteResult(true);
                }

                backup.Clean();
            }


        }

        public static void RevertChanges()
        {
            ConsoleTools.WriteError("Failed to install");
            if (!cFixMissing)
            {
                RestoreBackup();
                Console.Write("Cleaning up");
                if (backup.Clean())
                    ConsoleTools.WriteResult(true);
                else
                    ConsoleTools.WriteResult(false);
            }
            System.Environment.Exit(0);
        }

    }
}