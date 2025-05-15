using System;

namespace CMSAgent.Common.DTOs
{
    /// <summary>
    /// Information about an ignored update version
    /// </summary>
    public class IgnoredVersionInfo
    {
        /// <summary>
        /// Ignored version
        /// </summary>
        public string Version { get; set; } = string.Empty;
        
        /// <summary>
        /// Time when added to ignore list
        /// </summary>
        public DateTime AddedTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Reason for ignoring the version
        /// </summary>
        public string? Reason { get; set; }
        
        /// <summary>
        /// Number of failed update attempts
        /// </summary>
        public int FailedAttempts { get; set; } = 1;
    }
} 