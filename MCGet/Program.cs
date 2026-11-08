using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using MCGet.Platforms;
using System.Reflection;
using ConsoleTools;
using System.Text.RegularExpressions;
#if NET7_0_OR_GREATER
using System.Formats.Tar;
#endif

namespace MCGet
{
    public class Program
    {
        public enum COMMANDS { NONE = 0, INSTALL, SEARCH, LIST, REMOVE };

        public static string dir = "";
        public static string tempDir = "/temp/";
        public static string backupDir = "/backup/";
        public static string archiveDir = "/archives/";
        public static string api_user_agent = "";
        public static InstallLogger installLogger = new InstallLogger("");
        public static InstallationManager insManager = new InstallationManager();
        public static JsonDocument? manifestDoc = null;

        //command line args
        public static Flags flags = new Flags();
        public static COMMANDS command = COMMANDS.NONE;
        public static List<string> commandParams = new List<string>();

        static void Main(string[] args)
        {
            {
                string? informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                //cut commit hash
                informationalVersion = (informationalVersion?.Contains('+') ?? false) ? informationalVersion?.Substring(0, Math.Min(informationalVersion.IndexOf('+') + 8, informationalVersion.Length)) : informationalVersion;
                //only use informational version in dev builds
                informationalVersion = (informationalVersion?.Contains('-') ?? false) ? informationalVersion : null;
                api_user_agent = "mc-get/" + (informationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1") + " (ThunderClapLP/mc-get)";
            }
            CTools.ValidateConsole();
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8; //try to set output encoding to UTF8
            }
            catch (Exception) { }

            dir = AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
            if (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsMacOS())
            {
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/." + Assembly.GetExecutingAssembly().GetName().Name;
            }

            bool invalidArgs = true;
            string[] invalidArgsSuggestion = new string[] { "<archivepath>", "install <slug>" };
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--silent":
                    case "-s":
                        flags.Silent = true;
                        CTools.SilentMode = true;
                        break;
                    case "-h":
                    case "--help":
                        CTools.WriteLine(@"
Usage: 
  {ExecutableName} (flags) <archivepath>
  {ExecutableName} (flags) <command> (parameters)

Flags:
  -h, --help                :  displays this help page
  -s, --silent              :  performs a silent install. No user input needed
  -p, --platform <platform> :  installs from specified platform
                               either modrinth (mr) or curseforge (cf)
  -m, --mc-path <path>      :  specifies minecraft installation path
  --path <path>             :  specifies the target installation path
                               can also be used as a filter in other commands
  --mc-version <version>    :  specifies the minecraft version
  --server                  :  installs mod / modpack as server
  --set <name>=<value>      :  sets a setting to the specified value
  --unset <name>            :  resets a setting to its default value
  --get <name>              :  prints the value of a setting
  --version                 :  displays the current version

Commands:
  install <slug | id | name>:<mod(pack)version>:<modloader>
    installs a mod / modpack

  search <query>
    searches for modrinth/curseforge projects

  list installs
    lists all installed modpacks
  list mods <search>
    lists all custom mods in installation
    that fit the search term (either slug or id)

  remove installation <search>
    removes an installation that fits the search term (either slug or id)
    --path can also be used as a filter
  remove mod <installation> <mod>
    removes a mod from an installation
    both <installation> and <mod> are search terms (either slug or id)
    --path can also be used as a filter

Examples:
  {ExecutableName} install sodium:0.6.6:fabric
  {ExecutableName} --mc-version 1.19.3 install fabulously-optimized
  {ExecutableName} install fabulously-optimized
  {ExecutableName} -s install fabulously-optimized
  {ExecutableName} Fabulously.Optimized-4.10.5.mrpack
  {ExecutableName} list mods
  {ExecutableName} list mods fabulously-optimized
  {ExecutableName} remove installation 123
  {ExecutableName} remove installation fabulously-optimized
  {ExecutableName} remove mod fabulously-optimized sodium
".Replace("{ExecutableName}", Assembly.GetExecutingAssembly().GetName().Name));
                        Environment.Exit(0);
                        break;
                    case "-m":
                    case "--mc-path":
                        if (i < args.Length - 1)
                        {
                            insManager.currInstallation.minecraftDir = args[i + 1];
                            i++;
                        }
                        break;
                    case "--path":
                        if (i < args.Length - 1)
                        {
                            insManager.currInstallation.installationDir = args[i + 1];
                            i++;
                        }
                        break;
                    case "--mc-version":
                        if (i < args.Length - 1)
                        {
                            flags.MCVersion = args[i + 1];
                            i++;
                        }
                        break;
                    case "-p":
                    case "--platform":
                        if (i < args.Length - 1)
                        {
                            if (args[i + 1] == "cf" || args[i + 1].ToLower() == "curseforge")
                                flags.CurseForge = true;
                            else if (args[i + 1] == "mr" || args[i + 1].ToLower() == "modrinth")
                                flags.Modrinth = true;
                        }
                        if (!flags.CurseForge && !flags.Modrinth)
                        {
                            invalidArgs = true;
                            invalidArgsSuggestion = new string[] { args[i] + " mr", args[i] + " modrinth", args[i] + " cf", args[i] + " curseforge" };
                            CTools.WriteError("No valid platform specified. Expected curseforge or modrinth after " + args[i], 1);
                            i = args.Length; //exit the loop. break won't work because of the switch case
                        }
                        i++;
                        break;
                    case "--server":
                        flags.Server = true;
                        insManager.currInstallation.isServer = true;
                        break;
                    case "--set":
                        {
                            invalidArgs = true;
                            if (i < args.Length - 1)
                            {
                                int splitIndex = args[i + 1].IndexOf('=');
                                if (splitIndex > 0 && splitIndex < args[i + 1].Length - 1) //splitIndex exists and is not the first or last char
                                {
                                    insManager.LoadOrCreate(dir);
                                    string settingName = args[i + 1].Substring(0, splitIndex);
                                    string settingValue = args[i + 1].Substring(splitIndex + 1);
                                    invalidArgs = false;
                                    try
                                    {
                                        if (insManager.SetSetting(settingName, settingValue))
                                            CTools.WriteError("Setting \"" + settingName + "\" to \"" + settingValue + "\"", 0);
                                        else
                                            CTools.WriteError("Setting with the name \"" + settingName + "\" does not exist.", 1);
                                    }
                                    catch (Exception e)
                                    {
                                        CTools.WriteError(e.Message, 1);
                                    }
                                    insManager.Save();
                                }
                            }

                            if (invalidArgs)
                            {
                                invalidArgsSuggestion = new string[] { args[i] + " <setting name>=<value>" };
                                i = args.Length; //exit the loop. break won't work because of the switch case
                            }
                        }
                        break;
                    case "--unset":
                        {
                            invalidArgs = true;
                            if (i < args.Length - 1)
                            {
                                invalidArgs = false;
                                insManager.LoadOrCreate(dir);

                                if (insManager.UnsetSetting(args[i + 1]))
                                    CTools.WriteError("Resetting \"" + args[i + 1] + "\"", 0);
                                else
                                    CTools.WriteError("Setting with the name \"" + args[i + 1] + "\" does not exist.", 1);

                                insManager.Save();
                            }

                            if (invalidArgs)
                            {
                                invalidArgsSuggestion = new string[] { args[i] + " <setting name>" };
                                i = args.Length; //exit the loop. break won't work because of the switch case
                            }
                        }
                        break;
                    case "--get":
                        {
                            invalidArgs = true;
                            if (i < args.Length - 1)
                            {
                                invalidArgs = false;
                                insManager.LoadOrCreate(dir);

                                string? setting = insManager.installations.settings.GetOrNull(args[i + 1]);
                                if (setting != null)
                                    CTools.WriteLine("Setting\n  \"" + args[i + 1] + "\" = \"" + setting + "\"");
                                else
                                    CTools.WriteError("Setting with the name \"" + args[i + 1] + "\" does not exist.", 1);

                                insManager.Save();
                            }

                            if (invalidArgs)
                            {
                                invalidArgsSuggestion = new string[] { args[i] + " <setting name>" };
                                i = args.Length; //exit the loop. break won't work because of the switch case
                            }
                        }
                        break;
                    case "--version":
                        {
                            string? informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                            //cut commit hash
                            informationalVersion = (informationalVersion?.Contains('+') ?? false) ? informationalVersion?.Substring(0, Math.Min(informationalVersion.IndexOf('+') + 8, informationalVersion.Length)) : informationalVersion;
                            //only use informational version in dev builds
                            informationalVersion = (informationalVersion?.Contains('-') ?? false) ? informationalVersion : null;
                            CTools.WriteLine(Assembly.GetExecutingAssembly().GetName().Name + " Version " + (informationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown"));
                            Environment.Exit(0);
                        }
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
                            for (i = i + 1; i < args.Length; i++)
                            {
                                commandParams.Add(args[i]);
                            }
                        }
                        break;
                    case "list":
                        commandParams.Clear();
                        for (i = i + 1; i < args.Length; i++)
                        {
                            commandParams.Add(args[i]);
                        }
                        if (commandParams.Count >= 1 && (commandParams[0] == "mods" || commandParams[0] == "installs"))
                        {
                            command = COMMANDS.LIST;
                            invalidArgs = false;
                        }
                        else
                        {
                            invalidArgsSuggestion = new string[] { "list installs", "list mods (search)" };
                        }
                        break;
                    case "remove":
                        commandParams.Clear();
                        for (i = i + 1; i < args.Length; i++)
                        {
                            commandParams.Add(args[i]);
                        }
                        if (commandParams.Count >= 1 && (commandParams[0] == "mod" || commandParams[0] == "installation"))
                        {
                            command = COMMANDS.REMOVE;
                            invalidArgs = false;
                        }
                        else
                        {
                            invalidArgsSuggestion = new string[] { "remove installation (<id> | <slug>)", "remove mod (<install id> | <install slug>) (<mod id> <mod slug>)" };
                        }
                        break;
                    default:
                        if (args[i].ToLower().EndsWith(".zip") || args[i].ToLower().EndsWith(".mrpack"))
                        {
                            insManager.currInstallation.archivePath = Path.GetFullPath(args[i]);
                            invalidArgs = false;
                        }
                        break;
                }
            }

            if (invalidArgs || args.Length <= 0)
            {
                CTools.WriteLine("Usage:");
                foreach (string line in invalidArgsSuggestion)
                {
                    CTools.WriteLine("  " + Assembly.GetExecutingAssembly().GetName().Name + " " + line);
                }
                CTools.WriteLine("  " + Assembly.GetExecutingAssembly().GetName().Name + " --help for all arguments");
                if (OperatingSystem.IsWindows())
                {
                    CTools.Write("Press any key to exit");
                    Console.ReadKey();
                }
                Environment.Exit(0);
                return;
            }

            installLogger = new InstallLogger(dir + backupDir);
            insManager.LoadOrCreate(dir);

            //load CurseForge settings
            CurseForge.apiurl = insManager.installations.settings.Get(Settings.CF_API_URL);
            CurseForge.apikey = insManager.installations.settings.Get(Settings.CF_API_KEY);

            //prepare
            if (command == COMMANDS.INSTALL || insManager.currInstallation.archivePath != "") //only if we need to install a pack
                Prepare();

            Spinner spinner = new Spinner(CTools.CursorTop);
            switch(command)
            {
                case COMMANDS.INSTALL:
                    {
                        string installLoader = "";
                        string installGameVersion = flags.MCVersion;
                        string installModVersion = "";
                        string installName = commandParams[0].Split(":")[0];
                        for (int j = 1; j < commandParams[0].Split(":").Length; j++)
                        {
                            if (new string[] {"forge", "neoforge", "fabric", "quilt"}.Any((e) => commandParams[0].Split(":")[j].ToLower() == e))
                            {
                                installLoader = commandParams[0].Split(":")[j].ToLower();
                            }
                            else
                            {
                                installModVersion = commandParams[0].Split(":")[j];
                            }
                        }
                        new Commands.Install(flags, spinner, insManager, installLogger, dir, tempDir, backupDir, archiveDir)
                            .Run(new Commands.InstallParams(commandParams.ToArray(), installLoader, installGameVersion, installModVersion, installName));
                    }
                    break;
                case COMMANDS.SEARCH:
                    {
                        new Commands.Search(flags, spinner, insManager).Run(new Commands.CommandParams(commandParams.ToArray()));
                    }
                    break;
                case COMMANDS.LIST:
                    {
                        Commands.ListParams.Type type = Commands.ListParams.Type.Invalid;
                        if (commandParams[0] == "installs")
                            type = Commands.ListParams.Type.Installs;
                        else if (commandParams[0] == "mods")
                            type = Commands.ListParams.Type.Mods;
                        
                        new Commands.List(flags, spinner, insManager)
                            .Run(new Commands.ListParams(commandParams.ToArray(), type, commandParams.Count > 1 ? commandParams[1] : ""));
                    }
                    Environment.Exit(0);
                    break;
                case COMMANDS.REMOVE:
                    {
                        Commands.RemoveParams.Type type = Commands.RemoveParams.Type.Invalid;
                        if (commandParams[0] == "installation")
                            type = Commands.RemoveParams.Type.Install;
                        else
                            type = Commands.RemoveParams.Type.Mod;
                        
                        new Commands.Remove(flags, spinner, insManager)
                            .Run(new Commands.RemoveParams(
                                commandParams.ToArray(),
                                type,
                                commandParams.Count > 1 ? commandParams[1] : "",
                                commandParams.Count > 2 ? commandParams[2] : "")
                            );
                    }
                    break;
                default:
                    if (insManager.currInstallation.archivePath != "")
                    {
                        //install archive
                        insManager.EnsureUniqueId(insManager.currInstallation);
                        string extractedName = Path.GetFileNameWithoutExtension(insManager.currInstallation.archivePath);

                        new Commands.Install(flags, spinner, insManager, installLogger, dir, tempDir, backupDir, archiveDir)
                            .Run(new Commands.InstallParams(commandParams.ToArray(), extractedName));
                    }
                    else
                    {
                        //do nothing - can happen when called with --set for example
                        Environment.Exit(0);
                    }
                     break;
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
                System.Environment.Exit(1);
                //RevertChanges();
            }

            if (!insManager.currInstallation.isServer)
            {
                bool confirmed = false;
                //try backup minecraft path
                if (insManager.currInstallation.minecraftDir == "")
                {
                    insManager.currInstallation.minecraftDir = insManager.installations.settings.Get(Settings.MINECRAFT_PATH);
                    if (insManager.currInstallation.minecraftDir != "" && Directory.Exists(insManager.currInstallation.minecraftDir))
                        confirmed = true; //don't ask user to confirm if configured already
                }

                if (insManager.currInstallation.minecraftDir == "")
                {
                    if (System.OperatingSystem.IsWindows())
                    {
                        if (Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft"))
                        {
                            insManager.currInstallation.minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "/.minecraft";
                        }
                    }
                    else if (System.OperatingSystem.IsLinux())
                    {
                        insManager.currInstallation.minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.minecraft";
                    }
                    else if (System.OperatingSystem.IsMacOS())
                    {
                        insManager.currInstallation.minecraftDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Library/Application Support/minecraft";
                    }
                }
                while (!confirmed)
                {
                    if (!Directory.Exists(insManager.currInstallation.minecraftDir) || !File.Exists(insManager.currInstallation.minecraftDir + "/launcher_profiles.json"))
                    {
                        if (insManager.currInstallation.minecraftDir != "")
                            CTools.WriteError($"\"" + insManager.currInstallation.minecraftDir + "\" is not a valid Minecraft directory!");
                        else
                            CTools.WriteError("Minecraft directory not found!");

                        if (flags.Silent)
                            Environment.Exit(0);

                        confirmed = false;
                    }
                    else
                    {
                        insManager.currInstallation.minecraftDir = Path.GetFullPath(insManager.currInstallation.minecraftDir);
                        CTools.WriteLine("Minecraft Directory Found: " + insManager.currInstallation.minecraftDir);
                        confirmed = CTools.ConfirmDialog("Use this Directory?", true);

                    }
                    if (!confirmed)
                    {
                        CTools.Write("Enter Minecraft Path: ");
                        insManager.currInstallation.minecraftDir = Console.ReadLine() + "";
                        if (insManager.currInstallation.minecraftDir != null && insManager.currInstallation.minecraftDir != "")
                            insManager.currInstallation.minecraftDir = Path.GetFullPath(insManager.currInstallation.minecraftDir);
                    }
                    else
                        insManager.installations.settings.Set(Settings.MINECRAFT_PATH, insManager.currInstallation.minecraftDir ?? ""); //set path in settings 
                }

                if (installLogger.log.minecraftPath + "" != insManager.currInstallation.minecraftDir)
                {
                    installLogger.Clean();
                }
                installLogger.SetMinecraftPath(insManager.currInstallation.minecraftDir + "");
            }
            else
            {
                if (insManager.currInstallation.installationDir == "")
                {
                    CTools.Write("Enter the server path (leave empty for current directory): ");
                    if (!flags.Silent)
                    {
                        try {insManager.currInstallation.installationDir = Console.ReadLine() + "";}
                        catch {CTools.WriteLine("Getting user input failed!"); }
                        if (insManager.currInstallation.installationDir == null || insManager.currInstallation.installationDir == "")
                            insManager.currInstallation.installationDir = ".";
                    }
                }
                insManager.currInstallation.installationDir = Path.GetFullPath(insManager.currInstallation.installationDir);
                try
                {
                    Directory.CreateDirectory(insManager.currInstallation.installationDir);
                }
                catch {}
            }
        }

        public static bool DownloadJavaIfNotPresent()
        {
            if (File.Exists(dir + "/java/jdk-21.0.6+7-jre/bin/java.exe") || File.Exists(dir + "/java/jdk-21.0.6+7-jre/bin/java") || File.Exists(dir + "/java/jdk-21.0.6+7-jre/Contents/Home/bin/java"))
            {
                CTools.WriteError("Found internal Java", 0);
                return true; //already downloaded
            }

            Process java = new Process();
            java.StartInfo.FileName = "java";
            java.StartInfo.Arguments = "-version";
            try
            {
                string output = "";
                java.StartInfo.RedirectStandardOutput = true;
                java.StartInfo.RedirectStandardError = true;
                java.Start();
                output = java.StandardOutput.ReadToEnd() + java.StandardError.ReadToEnd();
                java.WaitForExit();

                //check if java version
                Regex versionRegex = new Regex("\\d+\\.\\d+\\.\\d+");
                Match match = versionRegex.Match(output);
                if (match.Success)
                {
                    CTools.WriteError("Found Java: " + match.Value, 0);
                    string[] version = match.Value.Split(".");
                    if (int.Parse(version[0]) < 17)
                    {
                        CTools.WriteError("Java verion too old", 1);
                        throw new Exception("Version too old");
                    }
                }
            }
            catch (Exception)
            {
                string javaUrl = "";
                if (System.OperatingSystem.IsWindows())
                {
                    if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_windows_hotspot_21.0.6_7.zip";
                    else if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_aarch64_linux_hotspot_21.0.6_7.tar.gz";

                }
                else if (System.OperatingSystem.IsLinux())
                {
#if NET7_0_OR_GREATER
                    if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_linux_hotspot_21.0.6_7.tar.gz";
                    else if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_aarch64_linux_hotspot_21.0.6_7.tar.gz";
#else
                CTools.WriteError("mc-get needs to be build for .net 7.0 or later to download Java on linux!\nPlease manually install Java 17 or later in your distribution.", 1);
                return false;
#endif
                }
                else if (System.OperatingSystem.IsMacOS())
                {
#if NET7_0_OR_GREATER
                    if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.X64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_mac_hotspot_21.0.6_7.tar.gz";
                    else if (System.Runtime.InteropServices.RuntimeInformation.OSArchitecture == System.Runtime.InteropServices.Architecture.Arm64)
                        javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_aarch64_mac_hotspot_21.0.6_7.tar.gz";
#else
                CTools.WriteError("mc-get needs to be build for .net 7.0 or later to download Java on mac!\nPlease manually install Java 17 or later.", 1);
                return false;
#endif
                }
                if (javaUrl == "")
                        return false; //system not compatable
                Spinner spinner = new Spinner("Downloading Java", CTools.CursorTop);
                if (Networking.DownloadFile(javaUrl, dir + tempDir + "java.zip", spinner))
                {
                    try
                    {
                        if (!Directory.Exists(dir + "java/"))
                        {
                            Directory.CreateDirectory(dir + "/java/");
                        }

                        if (System.OperatingSystem.IsWindows())
                            ZipFile.ExtractToDirectory(dir + tempDir + "java.zip", dir + "/java/", true);
                        else if (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsMacOS())
                        {
                            //file is a tar.gz on linux and mac
#if NET7_0_OR_GREATER
                            TarFile.ExtractToDirectory(
                                new GZipStream(new FileStream(dir + tempDir + "java.zip", FileMode.Open, FileAccess.Read),
                                CompressionMode.Decompress, leaveOpen: false),
                                dir + "/java/", overwriteFiles: true);

                            //mark as executable on linux
                            if (System.OperatingSystem.IsLinux())
                                if (!File.GetUnixFileMode(dir + "/java/jdk-21.0.6+7-jre/bin/java").HasFlag(UnixFileMode.UserExecute))
                                    File.SetUnixFileMode(dir + "/java/jdk-21.0.6+7-jre/bin/java", UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.SetUser);
#else
                            //mark as executable on linux
                            //use File.SetUnixFileMode when using dotnet 8
                            //Process chmodProc = new Process();
                            //chmodProc.StartInfo.FileName = "chmod";
                            //chmodProc.StartInfo.Arguments = "+x \"" + dir + "/java/jdk-21.0.6+7-jre/bin/java\"";
                            //chmodProc.Start();
                            CTools.WriteResult(false, spinner);
                            return false;
#endif
                        }

                    }
                    catch (Exception e)
                    {
                        CTools.WriteLine(e.Message);
                        CTools.WriteResult(false, spinner);
                        return false;
                    }
                    CTools.WriteResult(true, spinner);
                    return true;
                }
                CTools.WriteResult(false, spinner);
            }

            return false;
        }
    }
}
