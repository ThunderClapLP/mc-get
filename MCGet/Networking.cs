using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using ConsoleTools;

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


            spinner?.StartAnimation();
            try
            {
                streamTask.Wait();
            }
            catch (System.AggregateException e)
            {
                CTools.WriteLine(e.Message);
                spinner?.StopAnimation();
                return false;
            }
            spinner?.StopAnimation();

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

            spinner?.StartAnimation();
            try
            {
                copyTask.Wait();
            }
            catch (System.AggregateException)
            {
                return false;
                //TODO: handle disk full seperatly (Abort all downloads). Maybe an enum as return type
            }
            spinner?.StopAnimation();

            return true;
        }
    }
}
