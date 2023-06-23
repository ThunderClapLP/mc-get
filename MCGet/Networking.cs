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
                //only ose useragent with modrinth
                client.DefaultRequestHeaders.UserAgent.ParseAdd(Program.api_user_agent);
            }
            Task<Stream> streamTask = client.GetStreamAsync(url);

            spinner?.Draw();
            while (!streamTask.IsCompleted)
            {
                for (int i = 0; i < 10 && !streamTask.IsCompleted; i++)
                {
                    Thread.Sleep(100);
                    if (i % 2 == 0)
                        spinner?.Update();

                }
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
                for (int i = 0; i < 10 && !copyTask.IsCompleted; i++)
                {
                    Thread.Sleep(100);
                    if (i % 2 == 0)
                        spinner?.Update();

                }
            }

            if (!copyTask.IsCompletedSuccessfully)
            {
                return false;
            }

            return true;
        }
    }
}
