using System;
using System.Reflection;
using System.Threading.Tasks;
using ConsoleTools;
using MCGet.Platforms;

namespace MCGet.Commands
{
    public class Search : Command<CommandParams>
    {
        public Search(Flags flags, Spinner spinner, InstallationManager insManager) : base(flags, spinner, insManager) {}

        public override void Run(CommandParams commandParams)
        {
            spinner.msg = "Searching for Projects";
            spinner.StartAnimation();
            Task<SearchResult> mrResult = Modrinth.SearchForProjects(string.Join(" ", commandParams.parameters));
            Task<SearchResult> cfResult = CurseForge.SearchForProjects(string.Join(" ", commandParams.parameters));
            mrResult.Wait();
            spinner.StopAnimation();
            spinner.Clean();
            CTools.ClearLine();
            if (mrResult.Result.success && mrResult.Result.results.Count > 0)
            {
                CTools.WriteLine("Modrinth projects:");
                mrResult.Result.results.ForEach((s) => CTools.WriteLine(" " + s));
            }
            else if (mrResult.Result.error == SearchResult.ErrorCode.Gone)
                CTools.WriteError("The used Modrinth API version is no longer available! Make sure to use the newest version of this software");
            else if (mrResult.Result.error == SearchResult.ErrorCode.ConnectionFailed)
                CTools.WriteError("Connection to Modrinth failed!", 1);

            spinner.top = CTools.CursorTop;
            spinner.msg = ""; //force printmsg
            spinner.msg = "Searching for Projects";
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
            else if (cfResult.Result.error == SearchResult.ErrorCode.ConnectionFailed)
                CTools.WriteError("Connection to CurseForge failed!", 1);
            else if (cfResult.Result.error == SearchResult.ErrorCode.ConnectionRefused)
            {
                CTools.WriteError("Connection to CurseForge refused! Did you set the API up correctly?", 1);
                CTools.WriteLine("  cfApiUrl=" + insManager.installations.settings.Get(Settings.CF_API_URL));
                if (insManager.installations.settings.Get(Settings.CF_API_KEY) == "")
                {
                    CTools.WriteLine("  cfApiKey is not set!");
                    CTools.WriteError("You can set the API key with: " + Assembly.GetExecutingAssembly().GetName().Name + " --set cfApiKey=<your key>", 0);
                }
                else
                {
                    CTools.WriteLine("  cfApiKey is set!");
                }
            }

            if (!mrResult.Result.success && !cfResult.Result.success)
            {
                spinner.top = CTools.CursorTop;
                spinner.msg = ""; //force printmsg
                spinner.msg = "Searching for Projects";
                spinner.Update(true);
                CTools.WriteResult(false, spinner);
            }

            if (mrResult.Result.results.Count == 0 && cfResult.Result.results.Count == 0)
                CTools.WriteLine("No projects found!");
            Environment.Exit(0);
        }
    }
}