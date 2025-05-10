# CMS Agent (.NET)

This project is a .NET conversion of the original Python-based CMS Agent and Updater.

## Projects

- **CMSAgent**: The main agent application, designed to run as a Windows Service. It handles communication with the server, monitors the system, executes commands, and manages self-updates.
- **CMSUpdater**: A helper application responsible for applying updates to the CMSAgent.

## Structure

(Refer to `convert_agent_to_dotnet.md` for detailed original structure and conversion plan.)

### CMSAgent

- `Program.cs`: Main entry point, handles CLI arguments (install, uninstall, configure, console mode) and service bootstrapping.
- `AgentService.cs`: Inherits from `ServiceBase` for Windows Service lifecycle management.
- `Core/`: Contains the core agent logic, including state management, command execution, and update handling.
- `Communication/`: Handles HTTP and WebSocket communication with the server.
- `Configuration/`: Manages agent configuration (`agent_config.json`) and runtime state.
- `Monitoring/`: Gathers system hardware and usage information.
- `CommandHandlers/`: Implements different types of command handlers.
- `SystemOperations/`: Provides utilities for Windows-specific operations (service installation, sync, etc.).
- `UserInterface/`: Handles console interactions for setup and reconfiguration.
- `Utilities/`: Common utility functions (file operations, logging setup).
- `Models/`: Data models used throughout the agent.
- `agent_config.json`: Configuration file for the agent.
- `appsettings.json`: Application settings, primarily for logging.

### CMSUpdater

- `Program.cs`: Main entry point for the updater. Handles downloading, backing up, extracting, and applying updates.
- `appsettings.json`: Application settings for the updater, primarily for logging.

## Setup & Usage

### Prerequisites

- .NET 8 SDK (or newer, as per project configuration)

### Building

1. Open `CMSAgent.sln` in Visual Studio or use the .NET CLI.
2. Navigate to the `dotnet_agent` directory in your terminal.
3. Build the solution: `dotnet build CMSAgent.sln`

### Installation (as a Windows Service)

Ensure you are running PowerShell as an Administrator.

```powershell
# Navigate to the build output directory for CMSAgent 
# (e.g., .\CMSAgent\bin\Debug\net8.0-windows or .\CMSAgent\bin\Release\net8.0-windows)
cd .\CMSAgent\bin\Debug\net8.0-windows

# Install the service
.\CMSAgent.exe --install

# Start the service (optional, can also be done via services.msc)
Start-Service -Name CMSAgent
```

### Configuration

Run the agent with the `--configure` flag to set up initial parameters like room position. This should be done from the directory where `CMSAgent.exe` is located.

```powershell
# Navigate to the build output directory for CMSAgent
.\CMSAgent.exe --configure
```

Edit `agent_config.json` and `appsettings.json` in the installation directory (where `CMSAgent.exe` resides after installation/build) for further customization.

### Running in Console Mode (for debugging)

This mode is useful for development and debugging.

```powershell
# Navigate to the build output directory for CMSAgent
.\CMSAgent.exe --console
```

### Uninstallation

Ensure you are running PowerShell as an Administrator.

```powershell
# Navigate to the build output directory for CMSAgent
.\CMSAgent.exe --uninstall
```

## Development Notes

- Logging is configured using Serilog. Logs are written to the console (in console mode) and to a `logs/` directory relative to the executable's location.
- The agent uses `SocketIOClient` for WebSocket communication.
- The `UpdateHandler` in `CMSAgent.Core` initiates the update process by launching `CMSUpdater.exe` with appropriate arguments.
- Ensure the `targetDir` argument for `CMSUpdater.exe` points to the root installation directory of `CMSAgent`.
