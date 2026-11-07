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
    public abstract class Command
    {
        /// <summary>
        /// Runs the command
        /// </summary>
        /// <param name="commandParams"></param>
        /// <returns>errorcode</returns>
        public abstract int Run(CommandParams commandParams);
    }
}