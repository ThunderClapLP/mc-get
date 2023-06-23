using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MCGet
{
    public class Networking
    {
        public static bool DownloadFile(string url, string targetPath, Spinner? spinner = null)
        {
            using HttpClient client = new HttpClient();
            if (Program.archPath.EndsWith(".mrpack"))
            {
                //only use useragent with modrinth
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);
            }
            Task<Stream> streamTask = client.GetStreamAsync(url);

            spinner?.Draw();
            while (!streamTask.IsCompleted)
            {
                streamTask.Wait(100);
                spinner?.Update();
            }

            if (!streamTask.IsCompletedSuccessfully)
            {
                return false;
            }

            //create directory if needed
            if (Path.GetDirectoryName(targetPath) != null && !Directory.Exists(Path.GetDirectoryName(targetPath))) {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                } catch {
                    return false;
                }
            }

            using FileStream fs = new FileStream(targetPath, FileMode.OpenOrCreate);
            Task copyTask = streamTask.Result.CopyToAsync(fs);

            spinner?.Draw();
            while (!copyTask.IsCompleted)
            {
                copyTask.Wait(100);
                spinner?.Update();
            }

            if (!copyTask.IsCompletedSuccessfully)
            {
                return false;
            }

            return true;
        }
    }
}
