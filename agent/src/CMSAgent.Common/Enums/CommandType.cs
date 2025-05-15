namespace CMSAgent.Common.Enums
{
    /// <summary>
    /// Các loại lệnh mà agent có thể nhận và thực thi.
    /// </summary>
    public enum CommandType
    {
        /// <summary>
        /// Lệnh thực thi qua console (cmd.exe hoặc PowerShell).
        /// </summary>
        CONSOLE,

        /// <summary>
        /// Lệnh hành động hệ thống (ví dụ: khởi động lại, tắt máy).
        /// </summary>
        SYSTEM_ACTION,
    }
}