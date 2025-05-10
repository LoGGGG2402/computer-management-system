using System;

namespace CMSAgent.CommandHandlers
{
    public enum CommandStatus
    {
        Success,
        Failed,
        Timeout,
        Canceled,
        UnhandledType 
    }

    /// <summary>
    /// Represents the result of a command execution by a command handler.
    /// This is an internal model used by CommandExecutor and passed to Agent.cs.
    /// Agent.cs will then map this to AgentCommandResultPayload for sending to the server.
    /// </summary>
    public class CommandResult
    {
        public string CommandId { get; } // Made init-only
        public string CommandType { get; } // Made init-only, crucial for agent:command_result

        public bool Success { get; set; }
        public string? Output { get; set; } // Represents stdout
        public string? Error { get; set; }  // Represents stderr
        public int ExitCode { get; set; }
        public CommandStatus Status { get; set; }
        
        public DateTime StartedAt { get; } // Added to calculate duration
        public DateTime CompletedAt { get; private set; }
        public TimeSpan ExecutionTime => CompletedAt - StartedAt;

        public CommandResult(string commandId, string commandType)
        {
            CommandId = commandId;
            CommandType = commandType; // Initialize CommandType
            StartedAt = DateTime.UtcNow; // Record start time
            CompletedAt = StartedAt;     // Initialize completed time
            
            // Defaults
            Success = false;
            ExitCode = -1; // Indicate not set or abnormal termination initially
            Status = CommandStatus.Failed; 
        }

        public void MarkCompleted(bool success, string? output, string? error, int exitCode, CommandStatus status)
        {
            CompletedAt = DateTime.UtcNow;
            Success = success;
            Output = output;
            Error = error;
            ExitCode = exitCode;
            Status = status;

            // Ensure Success property aligns with CommandStatus.Success
            if (status == CommandStatus.Success && !success)
            {
                Success = true; // If status is Success, then Success bool must be true
            }
            else if (status != CommandStatus.Success && success)
            {
                // If status is not Success, then Success bool should ideally be false,
                // but we prioritize the explicit 'success' parameter if it was true.
                // This case might indicate a slight mismatch in logic, but we'll allow it.
            }
        }
        
        // Fluent setters can be useful but might be overkill if MarkCompleted is comprehensive.
        // For now, focusing on MarkCompleted.

        // Example of how it might be used by a handler:
        // var result = new CommandResult(commandId, commandType);
        // ... execute command ...
        // result.MarkCompleted(true, "output", null, 0, CommandStatus.Success);
        // or
        // result.MarkCompleted(false, null, "error details", 1, CommandStatus.Failed);
    }
}
