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
        public static bool modifyExisting = false;
        public static string api_user_agent = "mc-get/" + (Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.1") + " (ThunderClapLP/mc-get)";
        public static Backup backup = new Backup("");
        public static InstallationManager insManager = new InstallationManager();

        //command line args
        public static bool cSilent = false;
        public static bool cServer = false;
        public static bool cModrinth = false;
        public static bool cCurseForge = false;
        public static string cMCVersion = "";
        public static COMMANDS command = COMMANDS.NONE;
        public static List<string> commandParams = new List<string>();

        public static JsonDocument? manifestDoc = null;
        public static string extractedName = "";
        static void Main(string[] args)
        {
            CTools.ValidateConsole();
            try
            {
                Console.OutputEncoding = System.Text.Encoding.UTF8; //try to set output encoding to UTF8
            } catch (Exception) {}

            dir = AppContext.BaseDirectory ?? System.IO.Directory.GetCurrentDirectory();
            if (System.OperatingSystem.IsLinux() || System.OperatingSystem.IsMacOS())
            {
                dir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/." + Assembly.GetExecutingAssembly().GetName().Name;
            }
            //dir = System.IO.Directory.GetCurrentDirectory();

            bool invalidArgs = true;
            string[] invalidArgsSuggestion = new string[] { "<archivepath>", "install <slug>" };
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--silent":
                    case "-s":
                        cSilent = true;
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
                            cMCVersion = args[i + 1];
                            i++;
                        }
                        break;
                    case "-p":
                    case "--platform":
                        if (i < args.Length - 1)
                        {
                            if (args[i + 1] == "cf" || args[i + 1].ToLower() == "curseforge")
                                cCurseForge = true;
                            else if (args[i + 1] == "mr" || args[i + 1].ToLower() == "modrinth")
                                cModrinth = true;
                        }
                        if (!cCurseForge && !cModrinth)
                        {
                            invalidArgs = true;
                            invalidArgsSuggestion = new string[] { args[i] + " mr", args[i] + " modrinth", args[i] + " cf", args[i] + " curseforge" };
                            CTools.WriteError("No valid platform specified. Expected curseforge or modrinth after " + args[i], 1);
                            i = args.Length; //exit the loop. break won't work because of the switch case
                        }
                        i++;
                        break;
                    case "--server":
                        cServer = true;
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
                                            CTools.WriteError("Setting with the name \"" + settingName + "\" deos not exist.", 1);
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
                                    CTools.WriteError("Setting with the name \"" + args[i + 1] + "\" deos not exist.", 1);

                                insManager.Save();
                            }

                            if (invalidArgs)
                            {
                                invalidArgsSuggestion = new string[] { args[i] + " <setting name>" };
                                i = args.Length; //exit the loop. break won't work because of the switch case
                            }
                        }
                        break;
                    case "-v":
                    case "--version":
                        CTools.WriteLine(Assembly.GetExecutingAssembly().GetName().Name + " Version " + Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown");
                        //TODO: remove -v in 0.5
                        if (args[i] == "-v")
                            CTools.WriteError("Deprecated! Use --version instead. -v will be removed in a future version.", 1);
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

            backup = new Backup(dir + backupDir);
            insManager.LoadOrCreate(dir);

            //load CurseForge settings
            CurseForge.apiurl = insManager.installations.settings.cfApiUrl;
            CurseForge.apikey = insManager.installations.settings.cfApiKey ?? CurseForge.apikey;

            //prepare
            if (command == COMMANDS.INSTALL || insManager.currInstallation.archivePath != "") //only if we need to install a pack
                Prepare();

            Spinner spinner = new Spinner(CTools.CursorTop);
            switch(command)
            {
                case COMMANDS.INSTALL:
                    {
                        string installLoader = "";
                        string installGameVersion = cMCVersion;
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
                        if (installName != "")
                        {

                            GetProjectResult? result = GetProject(installName, installGameVersion, installModVersion, installLoader,
                                (!cCurseForge && !cModrinth) ? null : (cModrinth ? typeof(Modrinth) : typeof(CurseForge)), spinner);
                            insManager.EnsureUniqueId(insManager.currInstallation);

                            //download archive
                            if (result != null && result.urls.Count > 0 && result.urls[0] != "")
                            {
                                extractedName = result.name;
                                insManager.currInstallation.slug = result.slug;
                                if (result.projectType == ProjectType.Modpack)
                                {
                                    //modpack

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
                                                string profilePath = insManager.installations.settings.minecraftPath;
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
                                else if (result.projectType == ProjectType.Mod)
                                {
                                    //single mod
                                    CTools.WriteError("Single mod: " + result.name, 0);
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
                                            insManager.currInstallation = existingInstallations[0];
                                        }
                                        else
                                        {
                                            ProfileHandler ph = new ProfileHandler();
                                            string profilePath = insManager.installations.settings.minecraftPath;
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
                                        CTools.WriteLine("No existing project to install mod into");
                                        Environment.Exit(1);
                                    }

                                    //get project again with mc version and loader from installation
                                    //TODO: decide if install pack/mod would be better to avoid double search
                                    result = GetProject(installName, ins.mcVersion ?? installGameVersion, installModVersion, ins.modloader?.Split("-")[0] ?? installLoader, result.platformType, spinner);
                                    if (result == null || result.urls.Count == 0 || result.urls[0] == "")
                                    {
                                        CTools.WriteLine("No compatible version of the mod found for this installation");
                                        Environment.Exit(1);
                                    }


                                    CTools.WriteLine("Install");
                                    CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(result.urls[0])));
                                    if (result.urls.Count > 1)
                                    {
                                        CTools.WriteLine("with dependencies:");
                                        for (int i = 1; i < result.urls.Count; i++)
                                        {
                                            CTools.WriteLine(" " + Path.GetFileName(HttpUtility.UrlDecode(result.urls[i])));
                                        }
                                    }
                                    CTools.WriteError("Compatible modloaders: " + result.loader, 0);
                                    if (!CTools.ConfirmDialog("Install single mod", true))
                                    {
                                        //user canceled
                                        Environment.Exit(0);
                                        return;
                                    }
                                    spinner.top = CTools.CursorTop;
                                    spinner.msg = "Downloading single mod";
                                    for (int i = 0; i < result.urls.Count; i++)
                                    {
                                        if (!Networking.DownloadFile(result.urls[i], dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(result.urls[i])), spinner))
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
                                    //copy mod

                                    CTools.Write("Copy mod");
                                    for (int i = 0; i < result.urls.Count; i++)
                                    {
                                        try
                                        {
                                            File.Copy(dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(result.urls[i])), ins.installationDir + "/mods/" + Path.GetFileName(HttpUtility.UrlDecode(result.urls[i])), true);
                                            custMod.files.Add("/mods/" + Path.GetFileName(HttpUtility.UrlDecode(result.urls[i])));
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
                                if ((result?.error == GetProjectResult.ErrorCode.NotFound))
                                    CTools.WriteError("No project with slug or id '" + installName + "' was found" + (installGameVersion != "" ? (" for version " + installGameVersion) : ""));
                                Environment.Exit(1);
                                return;
                            }
                        }
                    }
                    break;
                case COMMANDS.SEARCH:
                    {
                        CTools.Write("Searching for Projects");
                        spinner.StartAnimation();
                        Task<SearchResult> mrResult = Modrinth.SearchForProjects(string.Join(" ", commandParams));
                        Task<SearchResult> cfResult = CurseForge.SearchForProjects(string.Join(" ", commandParams));
                        mrResult.Wait();
                        spinner.StopAnimation();
                        spinner.Clean();
                        CTools.ClearLine();
                        if (mrResult.Result.success && mrResult.Result.results.Count > 0)
                        {
                                CTools.WriteLine("Modrinth projects:");
                            mrResult.Result.results.ForEach((s) => CTools.WriteLine(" " + s));
                        }
                        CTools.Write("Searching for Projects");
                        spinner.top = CTools.CursorTop;
                        spinner.StartAnimation();
                        cfResult.Wait();
                        spinner.StopAnimation();
                        spinner.Clean();
                        CTools.ClearLine();
                        if (cfResult.Result.success && cfResult.Result.results.Count > 0)
                        {
                            CTools.WriteLine("CurseForge projects:");
                            cfResult.Result.results.ForEach((s) => CTools.WriteLine(" " + s));
                        }
                        if (!mrResult.Result.success && !cfResult.Result.success)
                            CTools.WriteResult(false);

                        if (mrResult.Result.results.Count == 0 && cfResult.Result.results.Count == 0)
                            CTools.WriteLine("No projects found!");
                        Environment.Exit(0);
                    }
                    break;
                case COMMANDS.LIST:
                    {
                        if (commandParams[0] == "installs")
                        {
                            //list installations
                            ProfileHandler ph = new ProfileHandler();
                            string profilePath = insManager.installations.settings.minecraftPath;
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
                        else
                        {
                            //list mods
                            List<Installation> searchResult = commandParams.Count > 1 ? insManager.SearchInstallations(commandParams[1], true) : insManager.installations.installations;
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
                                string profilePath = insManager.installations.settings.minecraftPath;
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
                                            foreach (String file in mod.files)
                                            {
                                                CTools.WriteLine("    " + file);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    Environment.Exit(0);
                    break;
                case COMMANDS.REMOVE:
                    {
                        List<Installation> installationSerchResult = commandParams.Count > 1 ? insManager.SearchInstallations(commandParams[1], true) : insManager.installations.installations;
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
                            string profilePath = insManager.installations.settings.minecraftPath;
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
                            if (commandParams[0] == "installation")
                            {
                                //remove installation
                                if (cSilent || CTools.ConfirmDialog("Are you sure to permanently remove installation " + ins.modpackName + " (" + ins.Id + ") from your disk", false))
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
                                List<CustomMod> modSearchResult = commandParams.Count > 2 ? ins.customMods.FindAll((e) => (e.slug?.StartsWith(commandParams[2]) ?? false) || (e.projectId?.StartsWith(commandParams[2]) ?? false)) : ins.customMods;
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
                    }
                    insManager.Save();
                    Environment.Exit(0);
                    break;
                default:
                    if (insManager.currInstallation.archivePath != "")
                    {
                        //install archive
                        insManager.EnsureUniqueId(insManager.currInstallation);
                        extractedName = Path.GetFileNameWithoutExtension(insManager.currInstallation.archivePath);

                        SetupInstallDir();
                    }
                    else
                    {
                        //do nothing - can happen when called with --set for example
                        Environment.Exit(0);
                    }
                     break;
            }

            //not needed anymore?
            //RestoreBackup();

            CTools.Write("Cleaning up");
            if (backup.Clean())
                CTools.WriteResult(true);
            else
                CTools.WriteResult(false);

            if (insManager.currInstallation.archivePath != "")
                CopyArchive();
            //backup.log.archiveFile = insManager.currInstallation.archivePath;

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
                platform.name = manifestDoc?.RootElement.GetOrNull("name").ToString() ?? "";
                insManager.currInstallation.modpackName = platform.name;
            }

            if (extractedName != "" && platform.name == "")
            {
                platform.name = extractedName;
                insManager.currInstallation.modpackName = extractedName;
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

            CopyOverrides();

            //if (!cServer) //do not backup on server
            //do not save backup at all
            //backup.Save();
            insManager.AddInstallation(insManager.currInstallation);
            insManager.Save();

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
                try { Console.ReadKey(); }
                catch {}
            }
            else
            {
                CTools.WriteLine();
            }


        }

        static GetProjectResult? GetProject(string installName, string installGameVersion, string installModVersion, string installLoader, Type? platformType, Spinner spinner)
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
            if (cfResult?.Result.error == GetProjectResult.ErrorCode.ConnectionFailed)
                CTools.WriteError("Connection to CurseForge failed", 1);
            else if (cfResult?.Result.error == GetProjectResult.ErrorCode.ConnectionRefused)
            {
                CTools.WriteError("Connection to CurseForge refused! Did you set the API up correctly?", 1);
                CTools.WriteLine("  cfApiUrl=" + insManager.installations.settings.cfApiUrl);
                if ((insManager.installations.settings.cfApiKey ?? "") == "")
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

        static void SetupInstallDir()
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
                        if (!(cSilent || CTools.ConfirmDialog("Continue anyway?", false)))
                        {
                            Environment.Exit(1);
                        }
                        insManager.currInstallation.installationDirWasEmpty = false;
                    }
                }
            }

            if (insManager.currInstallation.installationDir == "")
            {
                if (CTools.ConfirmDialog("Install modpack into \"" + InstallationManager.LocalToGlobalPath(insManager.installations.settings.defaultInstallationPath).Replace("\\", "/") + "\"?", true))
                {
                    insManager.currInstallation.installationDir = insManager.installations.settings.defaultInstallationPath;
                    try
                    {
                        //make sure install dir exists for disk space calculation to work on linux
                        Directory.CreateDirectory(InstallationManager.LocalToGlobalPath(insManager.installations.settings.defaultInstallationPath));
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
                        insManager.installations.settings.defaultInstallationPath = insManager.currInstallation.installationDir;
                }
            }
            if (!modifyExisting && !insManager.currInstallation.isServer)
                insManager.currInstallation.installationDir = insManager.currInstallation.installationDir.Replace("\\", "/").TrimEnd('/') + "/" + insManager.currInstallation.slug + insManager.currInstallation.Id;

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

            if (!insManager.currInstallation.isServer)
            {
                bool confirmed = false;
                //try backup minecraft path
                if (insManager.currInstallation.minecraftDir == "")
                {
                    insManager.currInstallation.minecraftDir = insManager.installations.settings.minecraftPath;
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

                        if (cSilent)
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
                        insManager.installations.settings.minecraftPath = insManager.currInstallation.minecraftDir ?? ""; //set path in settings 
                }

                if (backup.log.minecraftPath + "" != insManager.currInstallation.minecraftDir)
                {
                    backup.Clean();
                    //backup = new Backup(backup.path);
                }
                backup.SetMinecraftPath(insManager.currInstallation.minecraftDir + "");
            }
            else
            {
                if (insManager.currInstallation.installationDir == "")
                {
                    CTools.Write("Enter the server path (leave empty for current directory): ");
                    if (!cSilent)
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

        //Copy archive to ensure --fix-missing works after the origional archive is deleted or moved by the user.
        static void CopyArchive()
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
                backup.log.archiveFile = Path.GetFileName(insManager.currInstallation.archivePath);
            }
            catch (Exception)
            {
                backup.log.archiveFile = insManager.currInstallation.archivePath; //use absolute path on failure
            }
        }

        static void ExtractArchive()
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
            catch (Exception e)
            {
                CTools.WriteResult(false, spinner);
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
                CTools.WriteError("mc-get needs to be build for .net 7.0 or later to download java on linux!\nPlease manually install java 17 or later in your distribution.", 1);
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
                CTools.WriteError("mc-get needs to be build for .net 7.0 or later to download java on mac!\nPlease manually install java 17 or later.", 1);
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

        static void BackupModsFolder()
        {
            string modsDir = Path.GetFullPath(insManager.currInstallation.installationDir + "/mods");
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
                        if (!backup.BackopMod(mod, true))
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

                    //TODO: decide what to do here. Force to delete?
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
                if (!modifyExisting)
                    backup.DeleteLauncherProfile();

                backup.updateProgress += (int progress) =>
                {
                    spinner.Update();
                };

                if (!backup.RestoreMods() || !backup.RestoreOverrides())
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

                backup.Clean();
            }


        }

        public static void RevertChanges()
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
            if (backup.Clean())
                CTools.WriteResult(true);
            else
                CTools.WriteResult(false);
            System.Environment.Exit(1);
        }

    }
}
