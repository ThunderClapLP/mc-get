using ConsoleTools;

namespace MCGet.Commands
{
    public class CommandParams
    {
        /// <summary>
        /// Raw Parameters of the command
        /// </summary>
        public string[] parameters { get; }

        public CommandParams(string[] parameters)
        {
            this.parameters = parameters;
        }
    }
    public abstract class Command<TCommandParams>
    where TCommandParams : CommandParams
    {
        public Spinner spinner;
        public Flags flags;
        public InstallationManager insManager;

        public Command(Flags flags, Spinner spinner, InstallationManager insManager)
        {
            this.flags = flags;
            this.spinner = spinner;
            this.insManager = insManager;
        }
        /// <summary>
        /// Runs the command
        /// </summary>
        /// <param name="commandParams"></param>
        public abstract void Run(TCommandParams commandParams);
    }
}