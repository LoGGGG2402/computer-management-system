#!/usr/bin/env python3
"""
Main entry point for the Computer Management System Agent.
This script initializes and starts the agent process.
"""
import os
import sys
import logging
import argparse
from agent import Agent

# Setup logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.FileHandler(os.path.join(os.path.dirname(os.path.dirname(__file__)), 'logs', 'agent.log')),
        logging.StreamHandler()
    ]
)
logger = logging.getLogger(__name__)

def parse_arguments():
    """Parse command line arguments."""
    parser = argparse.ArgumentParser(description='Computer Management System Agent')
    parser.add_argument('--config', type=str, default='../config/agent_config.json',
                        help='Path to configuration file')
    parser.add_argument('--debug', action='store_true',
                        help='Enable debug logging')
    return parser.parse_args()

def main():
    """Main function to start the agent."""
    args = parse_arguments()
    
    # Set debug level if requested
    if args.debug:
        logging.getLogger().setLevel(logging.DEBUG)
        logger.debug("Debug logging enabled")
    
    try:
        # Initialize and start the agent
        logger.info("Starting Computer Management System Agent")
        agent = Agent(config_path=args.config)
        agent.start()
    except KeyboardInterrupt:
        logger.info("Agent stopped by user")
    except Exception as e:
        logger.exception(f"Error running agent: {e}")
        return 1
    
    return 0

if __name__ == "__main__":
    sys.exit(main())