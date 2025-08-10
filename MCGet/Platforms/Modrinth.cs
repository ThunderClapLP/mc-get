using MCGet.ModLoaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ConsoleTools;

namespace MCGet.Platforms
{
    public class Modrinth : Platform
    {
        static string url = "https://api.modrinth.com/v2";
        public override bool DownloadMods()
        {
            List<JsonElement> failedMods = new List<JsonElement>();

            if (Program.manifestDoc != null && Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() > 0)
            {
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                bar.max = Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength();
                bar.Update();

                Spinner spinner = new Spinner(CTools.CursorTop);

                //get all mods and calculate required disk space in kilobytes
                long requiredSpace = 0;
                List<JsonElement> files = new List<JsonElement>();
                foreach (JsonElement file in Program.manifestDoc.RootElement.GetProperty("files").EnumerateArray())
                {
                    if (failedMods.Contains(file))
                        failedMods.Remove(file);

                    //make sure to only download mods for specified environment (server / client)
                    JsonElement? currElem = file.GetOrNull("env")?.GetOrNull(Program.insManager.currInstallation.isServer ? "server": "client");
                    if (currElem == null || currElem?.ToString() != "unsupported")
                    {
                        if (!files.Any(x => x.GetOrNull("path")?.GetString() == file.GetOrNull("path")?.GetString())) //check if file with same path already exists in list
                        {
                            files.Add(file);
                            requiredSpace += file.GetOrNull("fileSize")?.GetInt64() / 1024 ?? 0; //convert to kilobytes
                        }
                    }
                }

                //get free space on disc
                try
                {
                    string drivePath = InstallationManager.LocalToGlobalPath(Program.insManager.currInstallation.installationDir);
                    if (!Directory.Exists(drivePath))
                        drivePath = Directory.GetParent(drivePath)?.FullName ?? "";
                    long freeSpace = Math.Min(new DriveInfo(Program.dir).AvailableFreeSpace / 1024, new DriveInfo(drivePath).AvailableFreeSpace / 1024);

                    if (requiredSpace > freeSpace - 10000) { //10MB buffer to prevent issues
                        CTools.WriteError("Not enough disk space! " + Math.Max((requiredSpace - freeSpace - 10000) / 1024, 1) + " MB more requiered.");
                        Program.RevertChanges(); //cannot proceed
                        return false;
                    }
                    
                }
                catch (Exception)
                {
                    //failed to get free space. Ask user to check
                    CTools.WriteError("Could not get free disc space. Please make sure enough disc space is available before continuing!", 1);
                    if (!CTools.ConfirmDialog((requiredSpace + 10000) / 1024 + " MB is required. Continue?", true)) {
                        Program.RevertChanges(); //aborted
                        return false;
                    }
                }

                //download all mods
                foreach (JsonElement file in files)
                {

                    if (!DownloadMod(file.GetProperty("downloads").EnumerateArray().First().GetString() ?? "", Program.dir + Program.tempDir + "mods/" + file.GetProperty("path").ToString(), spinner))
                    {
                        failedMods.Add(file);
                    } else
                    {
                        downloadedMods.Add(file.GetProperty("path").ToString());
                    }

                    bar.value++;
                    bar.Update();
                }
                bar.Clear();

                foreach (JsonElement file in failedMods)
                {
                    try
                    {
                        if (file.GetProperty("hashes").GetProperty("sha512").ToString() != null)
                            Program.backup.AddFailedMod(file.GetProperty("hashes").GetProperty("sha512").ToString() + "");
                    }
                    catch (Exception) { }
                }

                CTools.ClearLine();
                CTools.Write("Download finished!");
                if (failedMods.Count > 0)
                {
                    if (!CTools.ConfirmDialog(" " + failedMods.Count + " / " + Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() + " mods failed. Continue?", true))
                    {
                        Program.RevertChanges();
                        return false;
                    }
                }
                CTools.WriteLine();
            }

            return true;
        }

        public bool DownloadMod(string url, string destinationPath, Spinner? spinner = null)
        {
            if (spinner == null)
                spinner = new Spinner(CTools.CursorTop);
            spinner.top = CTools.CursorTop;

            //parse download url
            spinner.msg = "Downloading " + Path.GetFileName(destinationPath);

            if (url == "" || !Networking.DownloadFile(url, destinationPath, spinner))
            {
                CTools.WriteResult(false, spinner);
                return false;
            }

            CTools.WriteResult(true, spinner);
            return true;
        }

        public override bool InstallDependencies()
        {
            //get modloader
            string modloaderVersion = "";
            ModLoader? modLoader = null;
            try
            {
                if (Program.manifestDoc != null)
                {
                    JsonElement loader;
                    if (Program.manifestDoc.RootElement.GetProperty("dependencies").TryGetProperty(Encoding.UTF8.GetBytes("fabric-loader"), out loader))
                    {
                        modloaderVersion = "fabric-" + loader.GetString();
                        modLoader = new Fabric();
                    }
                    else if (Program.manifestDoc.RootElement.GetProperty("dependencies").TryGetProperty(Encoding.UTF8.GetBytes("forge"), out loader))
                    {
                        modloaderVersion = "forge-" + loader.GetString();
                        modLoader = new Forge();
                    }
                    else if (Program.manifestDoc.RootElement.GetProperty("dependencies").TryGetProperty(Encoding.UTF8.GetBytes("neoforge"), out loader))
                    {
                        modloaderVersion = "neoforge-" + loader.GetString();
                        modLoader = new NeoForge();
                    }
                    else if (Program.manifestDoc.RootElement.GetProperty("dependencies").TryGetProperty(Encoding.UTF8.GetBytes("quilt-loader"), out loader))
                    {
                        modloaderVersion = "quilt-" + loader.GetString();
                        modLoader = new Quilt();
                    }
                }
            }
            catch { }

            if (modloaderVersion == "" || modLoader == null)
            {
                CTools.WriteError("Could not find a compatible modloader");
                return false;
            }

            Program.insManager.currInstallation.modloader = modloaderVersion;
            Program.insManager.currInstallation.mcVersion = Program.manifestDoc?.RootElement.GetProperty("dependencies").GetProperty("minecraft").GetString();

            return InstallModloader(modLoader, modloaderVersion);
        }

        public static async Task<GetProjectResult> GetProject(string name, string minecraftVersion, string modVersion, string loader = "")
        {
            GetProjectResult result = new GetProjectResult(typeof(Modrinth));
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);

            //get mod
            Task<string> getTask = client.GetStringAsync(url + "/project/" + HttpUtility.UrlEncode(name));

            //wait for completion
            try
            {
                await getTask;
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    result.error = GetProjectResult.ErrorCode.NotFound;
                else if (e.StatusCode == System.Net.HttpStatusCode.Gone)
                    result.error = GetProjectResult.ErrorCode.Gone;
                else
                    result.error = GetProjectResult.ErrorCode.ConnectionFailed;
            }
            catch (TaskCanceledException)
                { result.error = GetProjectResult.ErrorCode.ConnectionFailed; }

            if (!getTask.IsCompletedSuccessfully || getTask.IsFaulted)
                return result;

            JsonDocument doc = JsonDocument.Parse(getTask.Result);

            if (doc.RootElement.GetProperty("versions").EnumerateArray().ToArray().Length == 0)
            {
                result.error = GetProjectResult.ErrorCode.NotFound;
                return result;
            }

            //get title and slug
            result.name = doc.RootElement.GetProperty("title").GetString() ?? "";
            result.slug = doc.RootElement.GetProperty("slug").GetString() ?? "";

            //get all versions
            getTask = client.GetStringAsync(url + "/project/" + HttpUtility.UrlEncode(name) + "/version");

            try
            {
                await getTask;
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    result.error = GetProjectResult.ErrorCode.NotFound;
                else if (e.StatusCode == System.Net.HttpStatusCode.Gone)
                    result.error = GetProjectResult.ErrorCode.Gone;
                else
                    result.error = GetProjectResult.ErrorCode.ConnectionFailed;
            }
            catch (TaskCanceledException)
                { result.error = GetProjectResult.ErrorCode.ConnectionFailed; }

            if (!getTask.IsCompletedSuccessfully || getTask.IsFaulted)
                return result;

            JsonDocument versionsDoc = JsonDocument.Parse(getTask.Result);
            IEnumerable<JsonElement> matchingVersions = versionsDoc.RootElement.EnumerateArray();

            //find correct minecraft version
            matchingVersions = versionsDoc.RootElement.EnumerateArray().Where((version) =>
            {
                bool match = true;
                if (match && minecraftVersion != "")
                    match = version.GetProperty("game_versions").EnumerateArray().Any((e) => e.GetString() == minecraftVersion);
                if (match && modVersion != "")
                    match = version.GetProperty("version_number").GetString()?.ToLower().Contains(modVersion.ToLower()) ?? false;
                if (match && loader != "")
                    match = version.GetProperty("loaders").EnumerateArray().Any((e) => e.GetString() == loader);
                return match;
            });
            JsonElement? matchingElement = null;
            if (matchingVersions.Count() > 0)
                matchingElement = matchingVersions.First();

            string resStr = matchingElement?.GetProperty("files").EnumerateArray().First().GetProperty("url").ToString() ?? "";

            //get dependencies
            if (matchingElement != null)
            {
                string loaderString = "";
                foreach (JsonElement suppLoader in matchingElement?.GetProperty("loaders").EnumerateArray()!)
                {
                    loaderString += suppLoader.ToString() + ",";
                }
                loaderString = loaderString.TrimEnd(',');
                result.loader = loaderString;
                switch (doc.RootElement.GetProperty("project_type").GetString()?.ToLower() ?? "")
                {
                    case "modpack":
                        result.projectType = ProjectType.Modpack;
                        break;
                    case "mod":
                        result.projectType = ProjectType.Mod;
                        break;
                    default:
                        result.projectType = ProjectType.Invalid;
                        break;
                }

                result.urls.Add(resStr);
                if (matchingElement?.TryGetProperty("dependencies", out JsonElement dependencies) ?? false)
                {
                    foreach (JsonElement dep in dependencies.EnumerateArray())
                    {
                        if (dep.GetProperty("dependency_type").ToString() == "required" && dep.GetProperty("version_id").ToString() != "")
                        {
                            result.urls.Add(await GetProjectFromVersion(dep.GetProperty("version_id").ToString()));
                        }
                    }
                }
            }

            if (resStr == "")
            {
                result.error = GetProjectResult.ErrorCode.NotFound;
            }
            else if (result.urls.Count > 0)
            {
                result.error = GetProjectResult.ErrorCode.None;
                result.success = true;
                return result;
            }

            return result;
        }

        public static async Task<string> GetProjectFromVersion(string version)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);
            Task<string> getTask = client.GetStringAsync(url + "/version/" + version);

            try
            {
                await getTask;
            }
            catch (System.Exception)
            {
                return "";
            }

            return JsonDocument.Parse(getTask.Result).RootElement.GetProperty("files").EnumerateArray().First().GetProperty("url").ToString() ?? "";
        }

        public static async Task<SearchResult> SearchForProjects(string search)
        {
            SearchResult result = new SearchResult();

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);

            Task<string> response = client.GetStringAsync(url + "/search?query=" + HttpUtility.UrlEncode(search));

            try
            {
                await response;
            }
            catch (HttpRequestException e) //catch network errors
            {
                result.success = false;
                if (e.StatusCode == System.Net.HttpStatusCode.Gone)
                    result.error = SearchResult.ErrorCode.Gone;
                else
                    result.error = SearchResult.ErrorCode.ConnectionFailed;
                return result;
            }
            catch (Exception)
            {
                result.success = false;
                result.error = SearchResult.ErrorCode.ConnectionFailed;
                return result;
            }

            JsonDocument json = JsonDocument.Parse(response.Result);

            foreach (JsonElement project in json.RootElement.GetProperty("hits").EnumerateArray())
            {
                result.results.Add(project.GetProperty("project_type") + ": " + project.GetProperty("slug") + " (" + project.GetProperty("title") + ")");
            }
            result.success = true;
            return result;
        }
    }
}
