using System;
using Microsoft.Extensions.Logging;
using CMSAgent.Common.Enums;

namespace CMSAgent.Core
{
    /// <summary>
    /// Quản lý trạng thái hiện tại của agent và thông báo khi có sự thay đổi trạng thái.
    /// </summary>
    public class StateManager
    {
        private readonly object _lock = new object();
        private AgentState _currentState = AgentState.INITIALIZING;
        private readonly ILogger<StateManager> _logger;

        /// <summary>
        /// Sự kiện được kích hoạt khi trạng thái agent thay đổi.
        /// </summary>
        public event Action<AgentState, AgentState> StateChanged;

        /// <summary>
        /// Lấy trạng thái hiện tại của agent.
        /// </summary>
        public AgentState CurrentState
        {
            get
            {
                lock (_lock)
                {
                    return _currentState;
                }
            }
        }

        /// <summary>
        /// Khởi tạo đối tượng StateManager.
        /// </summary>
        /// <param name="logger">Logger để ghi nhật ký.</param>
        public StateManager(ILogger<StateManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Thiết lập trạng thái mới cho agent.
        /// </summary>
        /// <param name="newState">Trạng thái mới.</param>
        public void SetState(AgentState newState)
        {
            AgentState oldState;
            
            lock (_lock)
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
