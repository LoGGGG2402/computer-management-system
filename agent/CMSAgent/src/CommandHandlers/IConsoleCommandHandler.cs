using CMSAgent.Models.Payloads;
using System.Threading.Tasks;

namespace CMSAgent.CommandHandlers
{
    public interface IConsoleCommandHandler : ICommandHandler
    {
        // HandleCommandAsync is inherited from ICommandHandler
        // No additional methods specific to console commands are defined here
        // as per the refactoring to align with Standard.md, where ConsoleCommandHandler
        // primarily executes shell commands via the generic HandleCommandAsync.
    }
}