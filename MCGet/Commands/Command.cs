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

        public Command(Flags flags, Spinner spinner)
        {
            this.flags = flags;
            this.spinner = spinner;
        }
        /// <summary>
        /// Runs the command
        /// </summary>
        /// <param name="commandParams"></param>
        public abstract void Run(TCommandParams commandParams);
    }
}