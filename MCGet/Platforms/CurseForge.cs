﻿using MCGet.ModLoaders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ConsoleTools;

namespace MCGet.Platforms
{
    public class CurseForge : Platform
    {
        static string apiurl = "";
        static string cfurl = "";
        public override bool DownloadMods()
        {
            List<JsonElement> failedMods = new List<JsonElement>();
            int tryCount = 0;

            if (Program.manifestDoc != null && Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() > 0)
            {
                ProgressBar bar = new ProgressBar(0, CTools.DockRight());
                bar.fill = true;
                bar.max = Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength();

                Spinner spinner = new Spinner(CTools.CursorTop);

                int failcount = 0;
                while ((tryCount < 4 && failcount > 0) || tryCount == 0)
                {
                    if (tryCount > 0)
                    {
                        bar.value = 0;
                        bar.max = failedMods.Count;
                        bar.Update();

                        CTools.Write("Retrying failed Mods. (Try " + tryCount + " / " + "3)");

                        bar.Update();

                        //spinner.top++;
                        for (int r = 0; r < 30; r++)
                        {
                            Thread.Sleep(200);
                            spinner.Update();
                        }

                        spinner.Clean();
                        CTools.WriteLine();
                        bar.Update();
                    }

                    failcount = 0;
                    foreach (JsonElement file in Program.manifestDoc.RootElement.GetProperty("files").EnumerateArray())
                    {
                        if (tryCount <= 0 || failedMods.Contains(file))
                        {
                            if (failedMods.Contains(file))
                                failedMods.Remove(file);

                            if (!Program.cFixMissing || Program.backup.IsModFailed(file.GetProperty("projectID").ToString() + ""))
                            {
                                if (!DownloadMod(file.GetProperty("projectID").ToString(), file.GetProperty("fileID").ToString(), Program.dir + Program.tempDir + "mods/", spinner))
                                {
                                    failedMods.Add(file);
                                    failcount++;
                                }
                            }
                            bar.value++;
                            bar.Update();
                        }
                    }
                    tryCount++;
                }
                bar.Clear();

                foreach (JsonElement file in failedMods)
                {
                    try
                    {
                        if (file.GetProperty("projectID").ToString() != null)
                            Program.backup.AddFailedMod(file.GetProperty("projectID").ToString() + "");
                    }
                    catch (Exception) { }
                }

                CTools.ClearLine();
                CTools.Write("Download finished!");
                if (failcount > 0)
                {
                    if (!CTools.ConfirmDialog(" " + failcount + " / " + Program.manifestDoc.RootElement.GetProperty("files").GetArrayLength() + " mods failed. Continue?", true))
                    {
                        Program.RevertChanges();
                    }
                }
                CTools.WriteLine();

            }
            else
            {
                CTools.WriteError("Mainifest does not include any files");
                Program.RevertChanges();
                return false;
            }
            return true;
        }

        bool DownloadMod(string projectId, string fileId, string destination, Spinner? spinner = null)
        {
            if (spinner == null)
                spinner = new Spinner(CTools.CursorTop);
            spinner.top = CTools.CursorTop;

            //parse download url
            spinner.msg = "fetching ";

            //try with curseforge api
            HttpClient client = new HttpClient(new HttpClientHandler() { AllowAutoRedirect = false });
            client.Timeout = new TimeSpan(0, 0, 10);
            string url = cfurl + "/api/v1/mods/" + projectId + "/files/" + fileId + "/download";

            int resCode = -1;
            Task<HttpResponseMessage>? res = null;
            while (resCode == -1 || resCode >= 300 && resCode <= 399)
            {
                if (res != null)
                {
                    url = res.Result.Headers.Location?.AbsoluteUri ?? "";
                    //Console.CursorLeft = 0;
                    string newMessage = "Downloading " + Path.GetFileName(HttpUtility.UrlDecode(Path.GetFileName(url))).Split("?")[0] + " ";
                    if (spinner!.msg != newMessage)
                        spinner!.msg = newMessage;
                    //client.Timeout = new TimeSpan(0, 0, 60); // TODO: handle timeout properly

                }
                res = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), HttpCompletionOption.ResponseHeadersRead);

                spinner?.Draw();
                spinner?.StartAnimation();
                try
                {
                    res.Wait();
                }
                catch (System.AggregateException) { }
                spinner?.StopAnimation();

                if (res.IsFaulted || res.IsCanceled) //handle cancel gracefully
                    break;

                resCode = (int)res.Result.StatusCode;


            }

            if (resCode == 200)
            {
                Task<byte[]>? bytes = res?.Result.Content.ReadAsByteArrayAsync();
                if (bytes != null)
                {
                    spinner?.StartAnimation();
                    try
                    {
                        bytes.Wait();
                    }
                    catch (System.AggregateException) { }
                    spinner?.StopAnimation();

                    if (bytes.IsCompleted && !bytes.IsFaulted && !bytes.IsCanceled)
                    {
                        string destFileName = Path.GetFileName(HttpUtility.UrlDecode(Path.GetFileName(url)));
                        //TODO: add to downloadedMods or Files list! seperate between mods, shaders, resourcepacks
                        string destinationPath = "";
                        if (destFileName.ToLower().EndsWith(".jar"))
                        {
                            //mod
                            destinationPath = "mods/" + destFileName;
                        }
                        else
                        {
                            //shader or resourcepack
                            client = new HttpClient();
                            url = apiurl + "/mods/" + projectId;

                            Task<String> tsk = client.GetStringAsync(url);

                            spinner?.Draw();
                            spinner?.StartAnimation();
                            try
                            {
                                tsk.Wait();
                            }
                            catch (System.AggregateException) { }
                            spinner?.StopAnimation();

                            if (tsk.IsFaulted)
                            {
                                CTools.WriteResult(false, spinner);
                                return false;
                            }

                            try
                            {
                                int ClassId = JsonDocument.Parse(tsk.Result).RootElement.GetProperty("data").GetProperty("classId").GetInt32();
                                switch (ClassId)
                                {
                                    case 6: //mod why ever it is not a .jar
                                        destinationPath = "mods/" + destFileName;
                                        break;
                                    case 12: //resourcepack
                                        destinationPath = "resourcepacks/" + destFileName;
                                        break;
                                    //case 17: //worlds
                                    //    destinationPath = "saves/" + destFileName;
                                    //    throw new Exception(); //not supported (needs to be extracted)
                                    //    break;
                                    case 6552: //shaderpack
                                        destinationPath = "shaderpacks/" + destFileName;
                                        break;
                                    case 6945: //datapack
                                    default:
                                        destinationPath = "";
                                        break;
                                        //throw new Exception(); //category not supported
                                }
                            }
                            catch (Exception e)
                            {
                                CTools.WriteResult(false, spinner);
                                return false;
                            }
                        }

                        if (destinationPath != "")
                        {
                            try
                            {
                                if (!Directory.Exists(Path.GetDirectoryName(destination + "/" + destinationPath)))
                                    Directory.CreateDirectory(Path.GetDirectoryName(destination + "/" + destinationPath)!);
                            }
                            catch
                            {
                                CTools.WriteResult(false, spinner);
                                return false;
                            }
                            System.IO.File.WriteAllBytes(destination + "/" + destinationPath, bytes.Result);
                            downloadedMods.Add(destinationPath);
                            CTools.WriteResult(true, spinner);
                        }
                        else { spinner?.Clean(); CTools.WriteError("Ignoring. Type not supported", 1); }

                        return true;
                    }
                }

            }

            CTools.WriteResult(false, spinner);
            return false;
        }

        public override bool InstallDependencies()
        {
            //get modloader
            string? modloaderVersion = "";
            try
            {
                if (Program.manifestDoc != null)
                {
                    foreach (JsonElement loader in Program.manifestDoc.RootElement.GetProperty("minecraft").GetProperty("modLoaders").EnumerateArray())
                    {
                        if (loader.GetProperty("primary").GetBoolean())
                        {
                            modloaderVersion = loader.GetProperty("id").GetString();
                        }
                    }
                }
            }
            catch { }

            if (modloaderVersion == null)
            {
                CTools.WriteError("Could not find a modloader");
                return false;
            }
            ModLoader? modLoader = null;

            if (modloaderVersion.StartsWith("forge"))
                modLoader = new Forge();
            else if (modloaderVersion.StartsWith("neoforge"))
                modLoader = new NeoForge();
            else if (modloaderVersion.StartsWith("fabric"))
                modLoader = new Fabric();
            else if (modloaderVersion.StartsWith("quilt"))
                modLoader = new Quilt();

            if (modLoader == null)
            {
                CTools.WriteError("Could not find a compatible modloader");
                return false;
            }


            ProfileHandler ph = new ProfileHandler();
            ph.CreateSnapshot(Program.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.FIRST);

            bool success = modLoader?.Install(Program.manifestDoc?.RootElement.GetProperty("minecraft").GetProperty("version").GetString() ?? "", modloaderVersion) ?? false;

            ph.CreateSnapshot(Program.minecraftDir + "/launcher_profiles.json", ProfileHandler.SnapshotNumber.SECOND);

            if (Program.cServer)
                return true;

            //Get new profile by comparing the profile list from before with the one from after the modloader install. Does nothing if the modloader profile already existed before
            string newProfile = ph.ComputeDifference().FirstOrDefault("");
            ph.LoadProfiles(Program.minecraftDir + "/launcher_profiles.json");
            if (newProfile == "") //try by version if difference failed. Installer propably overwrote a profile.
                newProfile = ph.GetProfilesByLoaderVersion(modloaderVersion.Split("-")[0], modloaderVersion.Split("-")[1]).FirstOrDefault("");

            if (newProfile != "")
            {
                //no error checks at the moment
                if (this.name != "")
                    ph.SetProfileName(newProfile, this.name); //use modpack name as profile name
                string newId = newProfile + "-" + new Random().Next();
                ph.SetProfieId(newProfile, newId);
                ph.SaveProfiles(Program.minecraftDir + "/launcher_profiles.json");
                Program.backup.log.modloaderProfile = newId;
            }

            return success;
        }

        //0 = Any
        //1 = Forge
        //2 = Cauldron
        //3 = LiteLoader
        //4 = Fabric
        //5 = Quilt
        //6 = NeoForge
        public static int GetModloaderType(string loader)
        {
            switch (loader.ToLower())
            {
                case "forge":
                    return 1;
                case "fabric":
                    return 4;
                case "quilt":
                    return 5;
                case "neoforge":
                    return 6;
                default:
                    return -1;
            }
        }

        public static async Task<GetProjectResult> GetProject(string name, string minecraftVersion, string modVersion, string loader = "")
        {
            GetProjectResult result = new GetProjectResult();
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            //get mod
            Task<string> getTask = client.GetStringAsync(apiurl + "/mods/search?gameId=432&slug=" + HttpUtility.UrlEncode(name));

            //wait for completion
            try
            {
                await getTask;
            }
            catch (HttpRequestException e)
            {
                if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                    result.error = GetProjectResult.ErrorCode.NotFound;
                else
                    result.error = GetProjectResult.ErrorCode.ConnectionFailed;
            }
            catch (TaskCanceledException e)
                { result.error = GetProjectResult.ErrorCode.ConnectionFailed; }

            if (!getTask.IsCompletedSuccessfully || getTask.IsFaulted)
                return result;

            JsonDocument doc = JsonDocument.Parse(getTask.Result);
            JsonElement? file = null;
            int type = -1;

            if (doc.RootElement.GetProperty("data").EnumerateArray().Count() > 0)
            {
                string fileParams = "";
                if (minecraftVersion != "")
                    fileParams += "gameVersion=" + HttpUtility.UrlEncode(minecraftVersion);
                if (loader != "")
                {
                    int loaderType = GetModloaderType(loader);
                    if (loaderType > 0)
                    {
                        if (fileParams != "")
                            fileParams += "&";
                        fileParams += "modLoaderType=" + HttpUtility.UrlEncode(loaderType.ToString());
                    }
                }
                if (fileParams != "")
                    fileParams = "?" + fileParams;
                getTask = client.GetStringAsync(apiurl + "/mods/" + doc.RootElement.GetProperty("data").EnumerateArray().First().GetProperty("id").GetInt32() + "/files" + fileParams);

                try
                {
                    await getTask;
                }
                catch (HttpRequestException e)
                {
                    if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                        result.error = GetProjectResult.ErrorCode.NotFound;
                    else
                        result.error = GetProjectResult.ErrorCode.ConnectionFailed;
                }

                if (!getTask.IsCompletedSuccessfully || getTask.IsFaulted)
                    return result;

                JsonDocument versionsDoc = JsonDocument.Parse(getTask.Result);

                if (versionsDoc.RootElement.GetProperty("data").EnumerateArray().Count() > 0)
                {
                    IEnumerable<JsonElement> newList = versionsDoc.RootElement.GetProperty("data").EnumerateArray();
                    if (modVersion != "")
                        newList = newList.Where((e) => e.GetProperty("fileName").GetString()?.ToLower().Contains(modVersion.ToLower()) ?? false);

                    if (newList.Count() > 0)
                        file = newList.First();
                }
            }

            if (file != null)
            {
                result.name = doc.RootElement.GetProperty("data").EnumerateArray().First().GetProperty("name").GetString() ?? "";
                type = doc.RootElement.GetProperty("data").EnumerateArray().First().GetProperty("classId").GetInt32();

                string? url = file?.GetOrNull("downloadUrl")?.GetString();

                if (Program.cServer && type  == 4471)
                {
                    getTask = client.GetStringAsync(apiurl + "/mods/" + doc.RootElement.GetProperty("data").EnumerateArray().First().GetProperty("id").GetInt32() + "/files/" + file?.GetProperty("serverPackFileId").GetInt32());

                    try
                    {
                        await getTask;
                    }
                    catch (HttpRequestException e)
                    {
                        if (e.StatusCode == System.Net.HttpStatusCode.NotFound)
                            result.error = GetProjectResult.ErrorCode.NotFound;
                        else
                            result.error = GetProjectResult.ErrorCode.ConnectionFailed;
                    }

                    if (!getTask.IsCompletedSuccessfully || getTask.IsFaulted)
                        return result;

                    JsonDocument serverDoc = JsonDocument.Parse(getTask.Result);
                    url = serverDoc.RootElement.GetOrNull("data")?.GetOrNull("downloadUrl")?.GetString();
                }

                if (url != null)
                {
                    if (type == 4471) //modpack
                        url = "modpack|" + url;
                    else if (type == 6) //mod
                        url = "mod|" + loader + "|" + url;
                    else
                        url = "unknown|" + url;

                    result.urls.Add(url);
                    result.success = true;
                }

            }

            return result;
        }

        public static async Task<SearchResult> SearchForProjects(string search)
        {
            SearchResult result = new SearchResult();

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);

            Task<string> response = client.GetStringAsync(apiurl + "/mods/search?gameId=432&categoryIds=6,4471&sortField=2&sortOrder=desc&searchFilter=" + HttpUtility.UrlEncode(search));

            try
            {
                await response;
            }
            catch (Exception) //catch network errors
            {
                result.success = false;
                return result;
            }

            JsonDocument json = JsonDocument.Parse(response.Result);

            foreach (JsonElement project in json.RootElement.GetProperty("data").EnumerateArray())
            {
                if (search.Split(" ").All((s) => (project.GetProperty("name").GetString()?.Contains(s) ?? false) || (project.GetProperty("slug").GetString()?.Contains(s) ?? false)))
                    result.results.Add((project.GetProperty("classId").GetInt32() == 6 ? "mod" : "modpack") + ": " + project.GetProperty("slug").GetString() + " (" + project.GetProperty("name").GetString() + ")");
            }
            result.success = true;
            return result;
        }
    }
}
