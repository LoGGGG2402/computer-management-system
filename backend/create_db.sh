#!/bin/bash




install_postgresql() {
    echo "Command 'psql' not found. Attempting to install PostgreSQL for Ubuntu/Debian..."

    
    if ! command -v apt &> /dev/null; then
        echo "Error: Command 'apt' not found. This script only supports automatic installation on Debian/Ubuntu-based systems."
        echo "Please install PostgreSQL manually."
        return 1 
    fi

    echo "Attempting to install PostgreSQL using apt (sudo required)..."
    
    sudo apt update && sudo apt install -y postgresql postgresql-contrib

    
    if [ $? -ne 0 ]; then
        echo "Error: PostgreSQL installation using apt failed."
        return 1 
    fi

    echo "PostgreSQL installation appears successful."
    return 0 
}


if ! command -v psql &> /dev/null
then
    if ! install_postgresql; then
        
        exit 1
    fi

    
    if ! command -v psql &> /dev/null
    then
        echo "Error: 'psql' is still not available after installation attempt."
        echo "Please check installation errors or install manually."
        exit 1
    fi
    echo "'psql' is now available."
fi




if [ -f .env ]; then
  
  export $(grep -v '^#' .env | xargs)
else
  echo "Error: .env file not found. Please create a .env file with database configuration variables."
  exit 1
fi


if [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ] || [ -z "$DB_NAME" ] || [ -z "$DB_HOST" ] || [ -z "$DB_PORT" ]; then
  echo "Error: One or more database environment variables (DB_USER, DB_PASSWORD, DB_NAME, DB_HOST, DB_PORT) are not set in the .env file."
  exit 1
fi


if ! id "postgres" &>/dev/null; then
    echo "Error: System user 'postgres' not found. PostgreSQL installation might be incomplete or non-standard."
    exit 1
fi


echo "Attempting to create database '$DB_NAME' and user '$DB_USER' on $DB_HOST:$DB_PORT..."
echo "Executing psql command as system user 'postgres' using sudo (requires sudo privileges)..."


SQL_COMMANDS=$(cat <<-EOSQL
    -- Create the application user specified in .env
    CREATE USER "$DB_USER" WITH PASSWORD '$DB_PASSWORD';
    -- Create the application database specified in .env, owned by the application user
    CREATE DATABASE "$DB_NAME" OWNER "$DB_USER";
    -- Grant all privileges on the new database to the new user
    GRANT ALL PRIVILEGES ON DATABASE "$DB_NAME" TO "$DB_USER";
    -- Optional: Display databases and users for confirmation
    \l
    \du
EOSQL
)


if [ "$DB_HOST" = "localhost" ] || [ "$DB_HOST" = "127.0.0.1" ]; then
  echo "Connecting locally using Unix socket (peer authentication)..."
  sudo -u postgres psql -d template1 --set ON_ERROR_STOP=on <<< "$SQL_COMMANDS"
  psql_exit_status=$?
else
  echo "Connecting to remote host $DB_HOST..."
  
  
  sudo -u postgres psql -h "$DB_HOST" -p "$DB_PORT" -d template1 --set ON_ERROR_STOP=on <<< "$SQL_COMMANDS"
  psql_exit_status=$?
fi


if [ $psql_exit_status -eq 0 ]; then
  echo "-----------------------------------------------------"
  echo "Success!"
  echo "User '$DB_USER' created."
  echo "Database '$DB_NAME' created with owner '$DB_USER'."
  echo "-----------------------------------------------------"
else
  echo "-----------------------------------------------------"
  echo "Error: Failed to execute SQL commands via sudo -u postgres (Exit code: $psql_exit_status)."
  echo "Please check the error messages above."
  echo "Ensure PostgreSQL is running and the 'postgres' system user can connect via peer authentication (check pg_hba.conf if needed)."
  echo "Also ensure the user running this script has sudo privileges."
  echo "-----------------------------------------------------"
  exit 1
fi

echo "Script finished."
