using CMSAgent.Models.Payloads;
using System.Threading.Tasks;

namespace CMSAgent.CommandHandlers
{
    /// <summary>
    /// Interface for system command handlers
    /// </summary>
    public interface ISystemCommandHandler : ICommandHandler
    {
        // HandleCommandAsync is inherited from ICommandHandler
        // Specific command methods like RestartAgentAsync, GetSystemInfoAsync etc.
        // are now private methods within SystemCommandHandler and invoked based on
        // CommandRequest.commandType by the HandleCommandAsync method.
    }
}