#!/usr/bin/env python3
"""
Main entry point for the Computer Management System Agent.
This script initializes and starts the agent process.
"""
import os
import sys
import argparse

# Add the parent directory to sys.path to ensure imports work correctly
parent_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, parent_dir)

# Import the configuration manager and logger
from src.config.config_manager import config_manager
from src.core.agent import Agent

def parse_arguments():
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description='Computer Management System Agent')
    default_config = os.path.join(parent_dir, 'config', 'agent_config.json')
    parser.add_argument('--config', type=str, default=default_config,
                        help='Path to configuration file')
    parser.add_argument('--debug', action='store_true',
                        help='Enable debug logging')
    parser.add_argument('--setup', action='store_true',
                        help='Run first-time setup')
    return parser.parse_args()

def main():
    """Main function to start the agent."""
    args = parse_arguments()
    
    # Initialize the configuration manager
    if not config_manager.initialize(args.config, args.debug):
        print(f"Failed to initialize configuration from: {args.config}")
        return 1
    
    # Run first-time setup if requested
    if args.setup:
        if not config_manager.setup_first_run():
            print("Setup aborted")
            return 1
    
    # Get the storage path from config
    storage_path = config_manager.get('storage_path')
    
    # Create logs directory if it doesn't exist
    logs_dir = os.path.join(storage_path, 'logs')
    os.makedirs(logs_dir, exist_ok=True)
    
    try:
        # Initialize and start the agent
        agent = Agent(config_manager)
        agent.start()
    except KeyboardInterrupt:
        print("Agent stopped by user")
    except Exception as e:
        print(f"Error running agent: {e}")
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main())