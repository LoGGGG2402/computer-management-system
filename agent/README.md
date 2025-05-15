# CMSAgent - Client Computer Management Agent

**Last updated:** May 12, 2025

## Introduction

CMSAgent is a powerful client application designed to run on Windows computers. Its main tasks are collecting system information, monitoring resources, securely communicating with a central server, executing remote commands, and automatically updating to new versions. CMSAgent is designed to operate stably as a Windows Service, ensuring background, continuous operation and automatic startup with the system.

This project includes source code for CMSAgent, the CMSUpdater process, and other supporting components.

## Key Features

- **System Information Collection:**
    - Gather detailed hardware information (OS, CPU, GPU, RAM, Disk).
    - Monitor resource usage status (CPU, RAM, Disk) in real-time.
- **Secure Communication with Server:**
    - Establish and maintain secure WebSocket connections (Socket.IO over WSS) with the central server.
    - Use authentication tokens (Agent Token) for all communications.
    - API communication via HTTPS.
- **Remote Command Execution:**
    - Receive and execute commands sent from the server (e.g., run console scripts, perform system actions).
    - Report results back to the server.
- **Automatic Updates:**
    - Check for new versions periodically or receive notifications from the server.
    - Download update packages, verify checksums.
    - Use a separate `CMSUpdater.exe` process to safely replace files and restart the service.
    - Support automatic rollback if the new version encounters issues during startup.
- **Operating as a Windows Service:**
    - Run in the background, continuously.
    - Start automatically with Windows.
    - Ensure only one instance of the agent runs on each machine.
- **Flexible Configuration:**
    - Use `appsettings.json` for the main agent configurations and logging (Serilog).
    - Use `runtime_config.json` for machine-specific identification information and tokens (created during the `configure` process).
- **Detailed Logging:**
    - Log to files, Windows Event Log, and console (when debugging).
    - Support multiple log levels.
    - Capability to collect logs remotely at the server's request.
- **Error Handling and Recovery:**
    - Retry mechanism for network errors.
    - Temporary storage (queue) of data when offline and resend when connection is restored.
    - Safe handling of critical errors.
- **Command Line Interface (CLI):**
    - `CMSAgent.exe configure`: Configure the agent initially or reconfigure it.
    - `CMSAgent.exe start/stop/uninstall/debug`: Manage service and debug.

## Project Directory Structure
```
agent/
├── src/
│   ├── CMSAgent/           # Main project (Windows Service & CLI)
│   ├── CMSUpdater/         # Project for the Updater process
│   ├── CMSAgent.Common/    # Shared library (DTOs, Enums, Constants, Interfaces)
│   └── Setup/              # Installation package script (e.g., Inno Setup)
├── tests/
│   ├── CMSAgent.UnitTests/
│   └── CMSUpdater.UnitTests/
│   └── CMSAgent.IntegrationTests/
├── docs/                     # Project documentation
├── scripts/                  # Support scripts
├── .gitignore
├── CMSAgent.sln
└── README.md                 # This file

```

## System Requirements and Dependencies

- **Supported operating systems:** Windows 10 (1903+), Windows 11, Windows Server 2016/2019/2022 (64-bit only).
- **.NET Runtime:** The .NET version the agent is compiled for (e.g., .NET 9.0 LTS).
- **External libraries:** See detailed versions in Section II of "Comprehensive CMSAgent v7.4 Documentation".

## Installation and Configuration

1. **Build the project:** Build the `CMSAgent.sln` solution to create the executable files `CMSAgent.exe` and `CMSUpdater.exe`.
2. **Create installation package:** Use the Inno Setup script in the `src/Setup/` directory to package the necessary files into a `Setup.CMSAgent.exe` file.
3. **Run the installer:** Execute `Setup.CMSAgent.exe` with Administrator privileges on the client machine.
4. **Initial configuration:**
    - After installation, the installer will automatically run the command:
    `"C:\Program Files\CMSAgent\CMSAgent.exe" configure`
    - Follow the instructions in the command-line interface to enter room information and coordinates. The agent will connect to the server for authentication and obtain an `agentToken`.
    - This information (except for the encrypted token) will be saved in `C:\ProgramData\CMSAgent\runtime_config\runtime_config.json`.
    - The main operational configurations of the agent (such as server URL, reporting interval) are set in `C:\Program Files\CMSAgent\appsettings.json` and can be manually edited if needed.
5. **The service will automatically start** after successful configuration.

## Using the Command Line Interface (CLI)

After installation, you can use `CMSAgent.exe` from the installation directory with the following parameters (Administrator privileges required for most commands):

- `CMSAgent.exe configure`: Reconfigure the agent.
- `CMSAgent.exe start`: Start the CMSAgent Service.
- `CMSAgent.exe stop`: Stop the CMSAgent Service.
- `CMSAgent.exe uninstall`: Uninstall the agent.
    - `CMSAgent.exe uninstall --remove-data`: Uninstall and remove all agent data.
- `CMSAgent.exe debug`: Run the agent in console mode for debugging.

## Logging

- Main Agent Service logs: `C:\ProgramData\CMSAgent\logs\agent_YYYYMMDD.log`
- Updater logs: `C:\ProgramData\CMSAgent\logs\updater_YYYYMMDD_HHMMSS.log`
- Configuration process logs: `C:\ProgramData\CMSAgent\logs\configure_YYYYMMDD_HHMMSS.log`
- Important events are also recorded in the Windows Event Log (Application log, Source: "CMSAgentService").

## Contribution

(This section can be supplemented if the project is open source and accepts community contributions, including guidelines on how to report bugs, suggest features, or send pull requests.)

## License

(Information about the project's license, e.g., MIT, Apache 2.0, or proprietary license.)

For more detailed information on operations, communications, configuration, security, and error handling, please refer to the complete documentation: **"Comprehensive Documentation: Operations, Communications, and Configuration of CMSAgent v7.4"**.