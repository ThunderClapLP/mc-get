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

            bool invalidArgs = true;
            string invalidArgsSuggestion = "(<archivepath> | install <slug>)";
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
    -h / --help         :  displays this help page
    -s / --silent       :  performs a silent install. No user input needed
    -mr / --modrinth    :  download from modrinth
    -cf / --curseforge  :  download from curseforge
    -m <path>           :  specifies minecraft installation path
    --path              :  specifies the target installation path
    -mc <version>       :  specifies the minecraft version
    --server            :  installs mod / modpack as server
    -v / --version      :  displays the current version

Commands:
    install (<slug> | <id> | <name>):<mod(pack)version>:<modloader>
        installs a mod / modpack

    search <query>
        searches for modrinth/curseforge projects
    
    list installs
        list all installed modpacks
    list mods <search>
        list all custom mods in installation
        that fits the search term (either slug or id)

    remove installation <search>
        removes an installation that fits the search term (either slug or id)
    remove mod <installation>:<mod>
        removes a mod from an installation
        both <installation> and <mod> are search terms (either slug or id)
    
Examples:
    {ExecutableName} install sodium:0.6.6:fabric
    {ExecutableName} -mc 1.19.3 install fabulously-optimized
    {ExecutableName} install fabulously-optimized
    {ExecutableName} -s install fabulously-optimized
    {ExecutableName} Fabulously.Optimized-4.10.5.mrpack
".Replace("{ExecutableName}", Assembly.GetExecutingAssembly().GetName().Name));
                        Environment.Exit(0);
                        break;
                    case "-m":
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
                    case "-mc":
                        if (i < args.Length - 1)
                        {
                            cMCVersion = args[i + 1];
                            i++;
                        }
                        break;
                    case "-mr":
                    case "--modrinth":
                        cModrinth = true;
                        break;
                    case "-cf":
                    case "--curseforge":
                        cCurseForge = true;
                        break;
                    case "--server":
                        cServer = true;
                        insManager.currInstallation.isServer = true;
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
                            for (int j = i + 1; j < args.Length; j++)
                            {
                                commandParams.Add(args[j]);
                            }
                        }
                        break;
                    case "list":
                        if (i < args.Length - 1 && (args[i+1] == "mods" || args[i+1] == "installs"))
                        {
                            command = COMMANDS.LIST;
                            commandParams.Clear();
                            invalidArgs = false;
                            for (int j = i + 1; j < args.Length; j++)
                            {
                                commandParams.Add(args[j]);
                            }
                        }
                        else
                        {
                            invalidArgsSuggestion = "list installs/mods (search)";
                        }
                        break;
                    case "remove":
                        if (i < args.Length - 1 && (args[i + 1] == "mod" || args[i + 1] == "installation"))
                        {
                            command = COMMANDS.REMOVE;
                            commandParams.Clear();
                            invalidArgs = false;
                            for (int j = i + 1; j < args.Length; j++)
                            {
                                commandParams.Add(args[j]);
                            }
                        }
                        else
                        {
                            invalidArgsSuggestion = "remove installation (<id> | <slug>)/mod (<install id> | <install slug>):(<mod id> <mod slug>)";
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
                CTools.WriteLine("Usage: " + Assembly.GetExecutingAssembly().GetName().Name + " " + invalidArgsSuggestion + "\n --help for all arguments");
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
            insManager.LoadOrCreate(dir);



            //prepare
            if (command != COMMANDS.SEARCH && command != COMMANDS.LIST) //skip on search
                Prepare();

            Spinner spinner = new Spinner(CTools.CursorTop);
            spinner.Update();
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
                            Task<GetProjectResult>? mrResult = null;
                            Task<GetProjectResult>? cfResult = null;
                            if (cModrinth || (!cModrinth && !cCurseForge))
                                mrResult = Modrinth.GetProject(installName, installGameVersion, installModVersion, installLoader);
                            if (cCurseForge || (!cModrinth && !cCurseForge))
                                cfResult = CurseForge.GetProject(installName, installGameVersion, installModVersion, installLoader);
                            spinner.msg = "Getting project info";
                            spinner.StartAnimation();
                            mrResult?.Wait();
                            cfResult?.Wait();
                            spinner.StopAnimation();
                            List<string>? urls = null;
                            insManager.EnsureUniqueId(insManager.currInstallation);

                            //CTools.WriteResult(true);
                            if ((mrResult?.Result.success ?? false) && (cfResult?.Result.success ?? false))
                            {
                                CTools.WriteResult(true, spinner);
                                char choice = CTools.ChoiceDialog("Install from (M)odrinth or (C)urseForge?", new char[] { 'm', 'c' }, 'm');
                                if (choice == 'm')
                                {
                                    urls = mrResult.Result.urls;
                                    extractedName = mrResult.Result.name;
                                    insManager.currInstallation.slug = mrResult.Result.slug;
                                }
                                else
                                {
                                    urls = cfResult.Result.urls;
                                    extractedName = cfResult.Result.name;
                                    insManager.currInstallation.slug = mrResult.Result.slug;
                                }
                            }
                            else if (mrResult?.Result.success ?? false)
                            {
                                CTools.WriteResult(true, spinner);
                                urls = mrResult.Result.urls;
                                extractedName = mrResult.Result.name;
                                insManager.currInstallation.slug = mrResult.Result.slug;
                            }
                            else if (cfResult?.Result.success ?? false)
                            {
                                CTools.WriteResult(true, spinner);
                                urls = cfResult.Result.urls;
                                extractedName = cfResult.Result.name;
                                insManager.currInstallation.slug = cfResult.Result.slug;
                            }

                            if (mrResult?.Result.error == GetProjectResult.ErrorCode.ConnectionFailed)
                                CTools.WriteError("Connection to Modrinth failed", 1);
                            if (cfResult?.Result.error == GetProjectResult.ErrorCode.ConnectionFailed)
                                CTools.WriteError("Connection to CurseForge failed", 1);

                            //download archive
                            if (urls != null && urls[0] != "")
                            {
                                if (urls[0].Split("|")[0] == "modpack")
                                {
                                    //modpack

                                    //check for existing installations
                                    List<Installation> existingInstallations = insManager.GetInstallationsBySlug(insManager.currInstallation.slug);
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
                                                int insRes = CTools.ListDialog("Choose installation to list custom mods of",
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
                                    if (insManager.currInstallation.isServer)
                                    {
                                        Installation? ins = insManager.installations.installations.Find((e) => e.installationDir == insManager.currInstallation.installationDir);
                                        if (ins != null)
                                        {
                                            modifyExisting = true;
                                            insManager.currInstallation = ins;
                                            CTools.WriteError("Upgrading Server", 0);
                                        }
                                    }

                                    if (insManager.currInstallation.installationDir == "")
                                    {
                                        if (CTools.ConfirmDialog("Install modpack into \"" + insManager.installations.settings.defaultInstallationPath + "\"?", true))
                                            insManager.currInstallation.installationDir = insManager.installations.settings.defaultInstallationPath;
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
                                        }
                                    }
                                    if (!modifyExisting && !insManager.currInstallation.isServer)
                                        insManager.currInstallation.installationDir = insManager.currInstallation.installationDir.Replace("\\", "/").TrimEnd('/') + "/" + insManager.currInstallation.slug + insManager.currInstallation.Id;

                                    urls[0] = urls[0].Split("|").Last(); //delete modloaders
                                    spinner.top = CTools.CursorTop;
                                    spinner.msg = "Downloading pack file";
                                    if (!Networking.DownloadFile(urls[0], dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0])), spinner))
                                    {
                                        CTools.WriteResult(false, spinner);
                                        Environment.Exit(0);
                                        return;
                                    }

                                    insManager.currInstallation.archivePath = dir + archiveDir + Path.GetFileName(HttpUtility.UrlDecode(urls[0]));
                                    CTools.WriteResult(true, spinner);
                                }
                                else if (urls[0].Split("|")[0] == "mod")
                                {
                                    //single mod
                                    //TODO: choose existing installation
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
                                    spinner.top = CTools.CursorTop;
                                    spinner.msg = "Downloading single mod";
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        if (!Networking.DownloadFile(urls[i], dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), spinner))
                                        {
                                            CTools.WriteResult(false, spinner);
                                            Environment.Exit(0);
                                            return;
                                        }
                                    }

                                    CTools.WriteResult(true, spinner);

                                    //copy mod

                                    CTools.Write("Copy mod");
                                    for (int i = 0; i < urls.Count; i++)
                                    {
                                        try
                                        {
                                            File.Copy(dir + tempDir + "mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), insManager.currInstallation.installationDir + "/mods/" + Path.GetFileName(HttpUtility.UrlDecode(urls[i])), true);
                                            //TODO: add to insManager.currInstallation.customMods
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
                                CTools.WriteResult(false, spinner);
                                if ((mrResult?.Result.error == GetProjectResult.ErrorCode.NotFound) || (cfResult?.Result.error == GetProjectResult.ErrorCode.NotFound))
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
                            foreach (Installation install in insManager.installations.installations)
                            {
                                if (profilePath != install.minecraftDir)
                                {
                                    profilePath = install.minecraftDir;
                                    ph.LoadProfiles(profilePath + "/launcher_profiles.json");
                                }
                                CTools.WriteLine(install.modpackName + "\n  slug: " + (install.slug ?? "??") + "\n  Id: " + install.Id + "\n  ProfileName: " + (ph.GetProfileName(install.modloaderProfile ?? "") ?? "??") + "\n  path: " + install.installationDir);

                            }
                        }
                        else
                        {
                            //list mods
                            List<Installation> searchResult = commandParams.Count > 1 ? insManager.SearchInstallations(commandParams[1], true) : insManager.installations.installations;
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
                        List<Installation> installationSerchResult = commandParams.Count > 1 ? insManager.SearchInstallations(commandParams[1].Split(":")[0], true) : insManager.installations.installations;
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
                            CTools.WriteLine("    " + CTools.LimitText("ID", int.MaxValue.ToString().Length, true) + " | ProfileName | slug");
                            int insRes = CTools.ListDialog("Choose installation",
                                installationSerchResult.Select((e) => {
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
                                        if (Directory.Exists(InstallationManager.LocalToGlobalPath(ins.installationDir)))
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
                                }

                            }
                            else
                            {
                                //remove mod
                                List<CustomMod> modSearchResult = commandParams.Count > 1 && commandParams[1].Split(":").Length >= 2 ? ins.customMods.FindAll((e) => (e.slug?.StartsWith(commandParams[1].Split(":")[1]) ?? false) || (e.projectId?.StartsWith(commandParams[1].Split(":")[1]) ?? false)) : ins.customMods;
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
                                    try
                                    {
                                        if (mod.files != null && mod.files.Length > 0)
                                        {
                                            foreach (string file in mod.files)
                                            {
                                                File.Delete(InstallationManager.LocalToGlobalPath(ins.installationDir + "/" + file));
                                            }
                                        }
                                        else
                                            CTools.WriteError("Mod does not contain any files", 1);
                                        ins.customMods.Remove(mod);
                                    }
                                    catch (Exception)
                                    {
                                        CTools.WriteError("Deletion of at least one file failed! This will lead to inconsistent behaviour.\nPlease try again or delete them manually: ");
                                        if (mod.files != null && mod.files.Length > 0)
                                        {
                                            foreach (string file in mod.files)
                                            {
                                                CTools.WriteLine(file);
                                            }
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
            if (!cServer || insManager.currInstallation.archivePath.EndsWith(".mrpack"))
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

            if (!cServer)
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
                    if (!Directory.Exists(insManager.currInstallation.minecraftDir) || !Directory.Exists(insManager.currInstallation.minecraftDir + "/versions"))
                    {
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
            if (File.Exists(dir + "/java/jdk-21.0.6+7-jre/bin/java.exe") || File.Exists(dir + "/java/jdk-21.0.6+7-jre/bin/java"))
            {
                CTools.WriteError("Found internal Java", 0);
                return true; //already downloaded
            }

            Process java = new Process();
            java.StartInfo.FileName = "java";
            java.StartInfo.Arguments = "--version";
            try
            {
                java.StartInfo.RedirectStandardOutput = true;
                java.Start();
                java.WaitForExit();

                //check if java version
                Regex versionRegex = new Regex("\\d+\\.\\d+\\.\\d+");
                Match match = versionRegex.Match(java.StandardOutput.ReadToEnd());
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
                    javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_windows_hotspot_21.0.6_7.zip";
                else if (System.OperatingSystem.IsLinux())
                {
#if NET7_0_OR_GREATER
                    javaUrl = "https://github.com/adoptium/temurin21-binaries/releases/download/jdk-21.0.6%2B7/OpenJDK21U-jre_x64_linux_hotspot_21.0.6_7.tar.gz";
#else
                    CTools.WriteError("mc-get needs to be build for .net 7.0 or later to download java on linux!\nPlease manually install java 17 or later in your distribution.", 1);
                    return false;
#endif
                }
                else
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
                        else if (System.OperatingSystem.IsLinux())
                        {
                            //file is a tar.gz on linux
#if NET7_0_OR_GREATER
                            TarFile.ExtractToDirectory(
                                new GZipStream(new FileStream(dir + tempDir + "java.zip", FileMode.Open, FileAccess.Read),
                                CompressionMode.Decompress, leaveOpen: false),
                                dir + "/java/", overwriteFiles: true);

                            //mark as executable on linux
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