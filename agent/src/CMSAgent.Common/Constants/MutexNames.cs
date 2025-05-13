namespace CMSAgent.Common.Constants
{
    /// <summary>
    /// Hằng số tên Mutex cho hệ thống CMSAgent.
    /// </summary>
    public static class MutexNames
    {
        /// <summary>
        /// Tên Mutex toàn cục để đảm bảo chỉ có một instance của CMSAgent được chạy.
        /// GUID cụ thể (E17A2F8D-9B74-4A6A-8E0A-3F9F7B1B3C5D) để tránh xung đột với các ứng dụng khác.
        /// </summary>
        public const string AgentSingleton = "Global\\CMSAgentSingletonMutex_E17A2F8D-9B74-4A6A-8E0A-3F9F7B1B3C5D";
    }
}