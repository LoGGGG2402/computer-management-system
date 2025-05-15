using System;
using Microsoft.Extensions.Logging;
using CMSAgent.Common.Enums;
using System.Threading;

namespace CMSAgent.Core
{
    /// <summary>
    /// Manages the current state of the agent and notifies when there are state changes.
    /// </summary>
    /// <param name="logger">Logger for logging.</param>
    public class StateManager(ILogger<StateManager> logger)
    {
        private readonly Lock _lock = new();
        private AgentState _currentState = AgentState.INITIALIZING;
        private readonly ILogger<StateManager> _logger = logger;

        /// <summary>
        /// Event triggered when the agent state changes.
        /// </summary>
        public event Action<AgentState, AgentState> StateChanged = delegate { };

        /// <summary>
        /// Gets the current state of the agent.
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
        /// Sets a new state for the agent.
        /// </summary>
        /// <param name="newState">The new state.</param>
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
            
            // Notify subscribers about the state change
            StateChanged?.Invoke(oldState, newState);
        }
    }
}
