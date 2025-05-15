using System;
using Microsoft.Extensions.Logging;
using CMSAgent.Common.Enums;
using System.Threading;

namespace CMSAgent.Core
{
    /// <summary>
    /// Quản lý trạng thái hiện tại của agent và thông báo khi có sự thay đổi trạng thái.
    /// </summary>
    /// <param name="logger">Logger để ghi nhật ký.</param>
    public class StateManager(ILogger<StateManager> logger)
    {
        private readonly Lock _lock = new();
        private AgentState _currentState = AgentState.INITIALIZING;
        private readonly ILogger<StateManager> _logger = logger;

        /// <summary>
        /// Sự kiện được kích hoạt khi trạng thái agent thay đổi.
        /// </summary>
        public event Action<AgentState, AgentState> StateChanged = delegate { };

        /// <summary>
        /// Lấy trạng thái hiện tại của agent.
        /// </summary>
        public AgentState CurrentState
        {
            get
            {
                using (_lock.EnterScope())
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Thiết lập trạng thái mới cho agent.
        /// </summary>
        /// <param name="newState">Trạng thái mới.</param>
        public void SetState(AgentState newState)
        {
            AgentState oldState;
            
            using (_lock.EnterScope())
            {
                if (_currentState == newState)
                {
                    return;
                }
                
                oldState = _currentState;
                _currentState = newState;
            }
            
            _logger.LogInformation("Agent state changed from {OldState} to {NewState}", oldState, newState);
            
            // Thông báo cho các subscriber về sự thay đổi trạng thái
            StateChanged?.Invoke(oldState, newState);
        }
    }
}
