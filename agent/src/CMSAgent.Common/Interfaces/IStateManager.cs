using System;
using CMSAgent.Common.Enums;

namespace CMSAgent.Common.Interfaces
{
    /// <summary>
    /// Interface để quản lý trạng thái của agent.
    /// </summary>
    public interface IStateManager
    {
        /// <summary>
        /// Sự kiện được kích hoạt khi trạng thái agent thay đổi.
        /// </summary>
        event Action<AgentState, AgentState> StateChanged;

        /// <summary>
        /// Lấy trạng thái hiện tại của agent.
        /// </summary>
        AgentState CurrentState { get; }

        /// <summary>
        /// Thiết lập trạng thái mới cho agent.
        /// </summary>
        /// <param name="newState">Trạng thái mới.</param>
        void SetState(AgentState newState);
    }
}
