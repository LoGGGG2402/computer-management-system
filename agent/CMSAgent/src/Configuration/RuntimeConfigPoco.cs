namespace CMSAgent.Configuration
{
    /// <summary>
    /// Represents the runtime state configuration of the agent
    /// This configuration is stored in runtime_config.json
    /// </summary>
    public class RuntimeConfigPoco
    {
        /// <summary>
        /// Unique identifier for the agent
        /// </summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>
        /// Room configuration
        /// </summary>
        public RoomConfig? RoomConfig { get; set; } = new RoomConfig();

        /// <summary>
        /// Encrypted agent authentication token
        /// </summary>
        public string AgentTokenEncrypted { get; set; } = string.Empty;
    }

    /// <summary>
    /// Room configuration
    /// </summary>
    public class RoomConfig
    {
        /// <summary>
        /// Name of the room
        /// </summary>
        public string RoomName { get; set; } = string.Empty;

        /// <summary>
        /// X position in the room
        /// </summary>
        public string PosX { get; set; } = string.Empty;

        /// <summary>
        /// Y position in the room
        /// </summary>
        public string PosY { get; set; } = string.Empty;
    }
}