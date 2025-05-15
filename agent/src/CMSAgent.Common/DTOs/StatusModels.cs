using System;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Resource status information of the client machine sent to the server.
    /// </summary>
    public class StatusUpdatePayload
    {
        /// <summary>
        /// CPU usage percentage.
        /// </summary>
        public double cpuUsage { get; set; }

        /// <summary>
        /// RAM usage percentage.
        /// </summary>
        public double ramUsage { get; set; }

        /// <summary>
        /// Primary disk usage percentage.
        /// </summary>
        public double diskUsage { get; set; }
    }
}