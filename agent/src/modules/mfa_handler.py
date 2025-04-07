"""
MFA handler module for the Computer Management System Agent.
This module provides functionality for handling MFA codes.
"""
import logging
import getpass
from typing import Optional

logger = logging.getLogger(__name__)

def prompt_for_mfa() -> str:
    """
    Prompt the user to enter an MFA code.
    
    Returns:
        str: The MFA code entered by the user
    """
    print("\n" + "="*50)
    print("COMPUTER MANAGEMENT SYSTEM - AGENT REGISTRATION")
    print("="*50)
    print("\nA Multi-Factor Authentication (MFA) code is required to register this agent.")
    print("Please contact your system administrator to obtain the MFA code.")
    print("Once you have received the code, enter it below.\n")
    
    while True:
        try:
            # Using input() as specified in the requirements
            mfa_code = input("Enter MFA code: ").strip()
            
            if not mfa_code:
                print("MFA code cannot be empty. Please try again.")
                continue
                
            # Basic validation - typically MFA codes are numeric and fixed length
            if not mfa_code.isdigit():
                print("MFA code must be numeric. Please try again.")
                continue
                
            # Once we get a valid code, return it
            logger.info("MFA code entered by user")
            return mfa_code
            
        except KeyboardInterrupt:
            logger.warning("MFA input interrupted by user")
            raise
        except Exception as e:
            logger.error(f"Error during MFA input: {e}")
            print("An error occurred. Please try again.")

def display_registration_success() -> None:
    """Display a success message after successful agent registration."""
    print("\n" + "="*50)
    print("AGENT REGISTRATION SUCCESSFUL")
    print("="*50)
    print("\nThis agent has been successfully registered with the management system.")
    print("The agent will now connect to the server and begin monitoring.\n")