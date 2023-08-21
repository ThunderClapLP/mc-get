using MCGet.ModLoaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
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
        List<String> downloadedMods = new List<String>();
        public override bool DownloadMods()
        {
            List<JsonElement> failedMods = new List<JsonElement>();

            if (Program.manifestDoc != null && Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() > 0)
            {
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                bar.max = Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength();

                Spinner spinner = new Spinner(CTools.CursorTop);

                //get all mods and calculate required disk space in kilobytes
                long requiredSpace = 0;
                List<JsonElement> files = new List<JsonElement>();
                foreach (JsonElement file in Program.manifestDoc.RootElement.GetProperty("files").EnumerateArray())
                {
                    if (failedMods.Contains(file))
                        failedMods.Remove(file);

                    if (!Program.cFixMissing || Program.backup.IsModFailed(file.GetProperty("hashes").GetProperty("sha512").ToString() + ""))
                    {
                        //make sure to only download client mods
                        JsonElement? clientElem = file.GetOrNull("env")?.GetOrNull("client");
                        if (clientElem == null || clientElem?.ToString() != "unsupported")
                        {
                            if (!files.Any(x => x.GetOrNull("path")?.GetString() == file.GetOrNull("path")?.GetString())) //check if file with same path already exists in list
                            {
                                files.Add(file);
                                requiredSpace += file.GetOrNull("fileSize")?.GetInt64() / 1024 ?? 0; //convert to kilobytes
                            }
                        }
                    }
                }

                //get free space on disc
                try
                {
                    long freeSpace = Math.Min(new DriveInfo(Program.dir).AvailableFreeSpace / 1024, new DriveInfo(Program.minecraftDir).AvailableFreeSpace / 1024);

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
            CTools.ClearLine();
            CTools.Write("Downloading " + Path.GetFileName(destinationPath));

            if (url == "" || !Networking.DownloadFile(url, destinationPath, spinner))
            {
                CTools.WriteResult(false);
                return false;
            }

            CTools.WriteResult(true);
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
            
            ProfileHandler ph = new ProfileHandler();
            ph.CreateSnapshot(Program.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.FIRST);

            bool success = modLoader?.Install(Program.manifestDoc?.RootElement.GetProperty("dependencies").GetProperty("minecraft").GetString() ?? "", modloaderVersion) ?? false;
            
            ph.CreateSnapshot(Program.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.SECOND);

            //Get new profile by comparing the profile list from before with the one from after the modloader install. Does nothing if the modloader profile already existed before
            string newProfile = ph.ComputeDifference().FirstOrDefault() ?? "";
            if (newProfile != "")
            {
                //no error checks at the moment
                ph.LoadProfiles(Program.minecraftDir + "/launcher_profiles.json");
                ph.SetProfileName(newProfile, Program.manifestDoc?.RootElement.GetOrNull("name").ToString() ?? "unknown"); //use modpack name as profile name
                string newId = newProfile + "-" + new Random().Next(0, 10000);
                ph.SetProfieId(newProfile, newId);
                ph.SaveProfiles(Program.minecraftDir + "/launcher_profiles.json");
                Program.backup.log.modloaderProfile = newId;
            }

            return success;
        }

        public override bool InstallMods()
        {
            string modsDir = Path.GetFullPath(Program.minecraftDir + "/mods");

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
                    if (Path.GetDirectoryName(Program.minecraftDir + "/" + file) != null && !Directory.Exists(Path.GetDirectoryName(Program.minecraftDir + "/" + file)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Program.minecraftDir + "/" + file)!);
                    }

                    if (file.StartsWith("mods/") && !File.Exists(modsDir + "/" + Path.GetFileName(file)))
                        Program.backup.BackopMod(modsDir + "/" + Path.GetFileName(file), false);

                    File.Move(Program.dir + Program.tempDir + "mods/" + file, Program.minecraftDir + "/" + file, true);
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

        public static List<string>? GetProject(string name, string minecraftVersion, string loader = "", Spinner? spinner = null)
        {
            List<string> result = new List<string>();
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);

            CTools.Write("Getting project info");

            //get mod
            Task<string> getTask = client.GetStringAsync(url + "/project/" + name);

            //wait for completion
            spinner?.StartAnimation();
            try
            {
                getTask.Wait();
            }
            catch (AggregateException e)
            {
                spinner?.StopAnimation();

                if (e.InnerException?.Message.Contains("404") == true) //HACK: catch project not found
                {
                    CTools.WriteResult(false);
                    return null;
                }
                CTools.WriteResult(false);
                CTools.WriteError("Connection to Modrinth failed");
                Environment.Exit(1); //Exit here to prevent no project found message from showing. I know this is bad
                return null;
            }
            spinner?.StopAnimation();

            JsonDocument doc = JsonDocument.Parse(getTask.Result);

            string[] versionstring = {"", "", "", ""}; //0: 5 versions; 1: 10 versions; 2: 20 versions
            int count = 0;

            JsonElement[] versions = doc.RootElement.GetProperty("versions").EnumerateArray().ToArray();

            if (minecraftVersion != "" || loader != "")
            {
                for (int i = versions.Length - 1; i > 0; i--)
                {
                    //yes I know this is bad
                    if (i >= versions.Length - 5) //5 versions
                    {
                        versionstring[0] += "\"" + versions[i].ToString() + "\",";
                    }
                    else if (i >= versions.Length - 15) // 10 versions
                    {
                        versionstring[1] += "\"" + versions[i].ToString() + "\",";
                    }
                    else if (i >= versions.Length - 35) //20 versions
                    {
                        versionstring[2] += "\"" + versions[i].ToString() + "\",";
                    }
                    else //rest
                    {
                        versionstring[3] += "\"" + versions[i].ToString() + "\",";
                    }
                    count++;
                }
            }
            else if (doc.RootElement.GetProperty("versions").EnumerateArray().Count() > 0)
            {
                versionstring[0] += "\"" + doc.RootElement.GetProperty("versions").EnumerateArray().Last() + "\",";
                count++;
            }

            versionstring[0] = "[" + versionstring[0].TrimEnd(',') + "]";
            versionstring[1] = "[" + versionstring[1].TrimEnd(',') + "]";
            versionstring[2] = "[" + versionstring[2].TrimEnd(',') + "]";
            versionstring[3] = "[" + versionstring[3].TrimEnd(',') + "]";

            if (count == 0)
            {
                CTools.WriteResult(false);
                CTools.WriteLine("project not found");
                return null;
            }

            //get all versions
            for (int i = 0; i < versionstring.Length && versionstring[i].Length > 3; i++)
            {
                getTask = client.GetStringAsync(url + "/versions?ids=" + versionstring[i]);

                spinner?.StartAnimation();
                try
                {
                    getTask.Wait();
                }
                catch (AggregateException e)
                {
                    spinner?.StopAnimation();

                    if (e.InnerException?.Message.Contains("404") == true) //HACK: catch project not found
                    {
                        CTools.WriteResult(false);
                        return null;
                    }
                    CTools.WriteResult(false);
                    CTools.WriteError("Connection to Modrinth failed");
                    Environment.Exit(1); //Exit here to prevent no project found message from showing. I know this is bad
                    return null;
                }
                spinner?.StopAnimation();

                //Console.WriteLine(getTask.Result);

                JsonDocument versionsDoc = JsonDocument.Parse(getTask.Result);
                JsonElement? matchingElement = null;

                //find correct minecraft version
                bool found = false;
                foreach (JsonElement version in versionsDoc.RootElement.EnumerateArray())
                {
                    foreach(JsonElement gameVersion in version.GetProperty("game_versions").EnumerateArray())
                    {
                        if (gameVersion.ToString() == minecraftVersion || minecraftVersion == "")
                        {
                            if (loader != "")
                            {
                                foreach (JsonElement suppLoader in version.GetProperty("loaders").EnumerateArray())
                                {
                                    if (loader.ToLower() == suppLoader.ToString().ToLower()) //special loader
                                    {
                                        matchingElement = version;
                                        found = true;
                                        break;
                                    }
                                }
                                if (found)
                                    break;
                            }
                            else
                            {
                                matchingElement = version;
                                found = true;
                                break;
                            }
                        }
                    }
                    if (found) {
                        break;
                    }
                }
                string resStr = matchingElement?.GetProperty("files").EnumerateArray().First().GetProperty("url").ToString() ?? "";

                //get dependencies
                spinner?.StartAnimation();
                if (matchingElement != null)
                {
                    string loaderString = "";
                    foreach (JsonElement suppLoader in matchingElement?.GetProperty("loaders").EnumerateArray()!)
                    {
                        loaderString += suppLoader.ToString() + ",";
                    }
                    loaderString = loaderString.TrimEnd(',');
                    resStr = doc.RootElement.GetProperty("project_type") + "|" + loaderString + "|" + resStr;

                    result.Add(resStr);
                    if (matchingElement?.TryGetProperty("dependencies", out JsonElement dependencies) ?? false)
                    {
                        foreach (JsonElement dep in dependencies.EnumerateArray())
                        {
                            if (dep.GetProperty("dependency_type").ToString() == "required" && dep.GetProperty("version_id").ToString() != "")
                            {
                                //Console.WriteLine(dep.ToString());
                                result.Add(GetProjectFromVersion(dep.GetProperty("version_id").ToString()));
                            }
                        }
                    }
                }
                spinner?.StopAnimation();

                if (resStr == "" && i == versionstring.Length - 1)
                {
                    CTools.WriteResult(false);
                }
                else if (result.Count > 0)
                {
                    CTools.WriteResult(true);
                    return result;
                }

            }

            return null;
        }

        public static string GetProjectFromVersion(string version)
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);
            Task<string> getTask = client.GetStringAsync(url + "/version/" + version);

            //spinner is handled in calling function
            try
            {
                getTask.Wait();
            }
            catch (System.Exception)
            {
                return "";
            }

            return JsonDocument.Parse(getTask.Result).RootElement.GetProperty("files").EnumerateArray().First().GetProperty("url").ToString() ?? "";
        }

        public static void SearchForProjects(string search, Spinner? spinner = null)
        {
            CTools.Write("Searching for projects");

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);

            Task<string> response = client.GetStringAsync(url + "/search?query=" + HttpUtility.UrlEncode(search));

            spinner?.StartAnimation();
            try
            {
                response.Wait();
            }
            catch (AggregateException) //catch network errors
            {
                spinner?.StopAnimation();
                CTools.WriteResult(false);
                return; 
            }
            spinner?.StopAnimation();

            CTools.WriteResult(true);

            JsonDocument json = JsonDocument.Parse(response.Result);

            foreach (JsonElement project in json.RootElement.GetProperty("hits").EnumerateArray())
            {
                CTools.WriteLine(project.GetProperty("project_type") + ": " + project.GetProperty("slug") + " (" + project.GetProperty("title") + ")");
            }
        }
    }
}
