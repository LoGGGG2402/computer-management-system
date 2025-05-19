#!/bin/bash

# Script to transfer and run setup_postgresql.sh on remote server, using environment variables

# Load environment variables from .env file
if [ -f .env ]; then
    export $(cat .env | grep -v '^#' | xargs)
fi

# Configuration variables
REMOTE_USER="ubuntu"       # Replace with SSH username
REMOTE_PATH="/tmp/setup_postgresql.sh"
LOCAL_SCRIPT_PATH="./postgresql_setup.sh"

# Check if environment variables are set
if [ -z "$DB_HOST" ] || [ -z "$DB_NAME" ] || [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
    echo "Error: Environment variables DB_HOST, DB_NAME, DB_USER, and DB_PASSWORD must be set."
    echo "Example: export DB_HOST='your_host' DB_NAME='mydb' DB_USER='myuser' DB_PASSWORD='mypassword'"
    exit 1
fi

# Check if local script file exists
if [ ! -f "$LOCAL_SCRIPT_PATH" ]; then
    echo "Error: File $LOCAL_SCRIPT_PATH does not exist."
    exit 1
fi

# Transfer script file to remote server
echo "Transferring $LOCAL_SCRIPT_PATH to $DB_HOST:$REMOTE_PATH..."
scp "$LOCAL_SCRIPT_PATH" "$REMOTE_USER@$DB_HOST:$REMOTE_PATH"
if [ $? -ne 0 ]; then
    echo "Error: Could not transfer file to remote server."
    exit 1
fi

# Run script on remote server with environment variables
echo "Running script on remote server with DB_NAME=$DB_NAME, DB_USER=$DB_USER..."
ssh "$REMOTE_USER@$DB_HOST" "chmod +x $REMOTE_PATH && DB_NAME='$DB_NAME' DB_USER='$DB_USER' DB_PASSWORD='$DB_PASSWORD' sudo -E bash $REMOTE_PATH && rm -f $REMOTE_PATH"

if [ $? -eq 0 ]; then
    echo "Complete! Script has been successfully run on the remote server."
else
    echo "Error: Could not run script on remote server."
    exit 1
fi