#!/bin/bash

# Script to automatically install and configure PostgreSQL on Ubuntu 22.04, using environment variables and scram-sha-256

# Check if environment variables are set
if [ -z "$DB_NAME" ] || [ -z "$DB_USER" ] || [ -z "$DB_PASSWORD" ]; then
    echo "Error: Environment variables DB_NAME, DB_USER, and DB_PASSWORD must be set."
    exit 1
fi

# Configuration variables
LISTEN_ADDRESS="*"  # Listen on all IP addresses

# Update system
echo "Updating package list..."
sudo apt update -y

# Install PostgreSQL and contrib package
echo "Installing PostgreSQL..."
sudo apt install -y postgresql postgresql-contrib

# Automatically find the latest PostgreSQL version
echo "Finding PostgreSQL version..."
PG_VERSION=$(ls /etc/postgresql/ | sort -Vr | head -n 1)
if [ -z "$PG_VERSION" ]; then
    echo "Error: Could not find PostgreSQL version."
    exit 1
fi
echo "PostgreSQL version: $PG_VERSION"

# Check PostgreSQL service status
echo "Checking PostgreSQL status..."
sudo systemctl status postgresql --no-pager

# Start and enable PostgreSQL
echo "Ensuring PostgreSQL is running..."
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Switch to postgres user and create database/user
echo "Creating database and user..."
sudo -u postgres psql -c "CREATE DATABASE $DB_NAME;"
sudo -u postgres psql -c "CREATE USER $DB_USER WITH ENCRYPTED PASSWORD '$DB_PASSWORD';"
sudo -u postgres psql -c "GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;"

# Grant additional permissions
echo "Granting additional permissions..."
sudo -u postgres psql -d $DB_NAME -c "GRANT ALL ON SCHEMA public TO $DB_USER;"
sudo -u postgres psql -d $DB_NAME -c "GRANT ALL ON ALL TABLES IN SCHEMA public TO $DB_USER;"
sudo -u postgres psql -d $DB_NAME -c "GRANT ALL ON ALL SEQUENCES IN SCHEMA public TO $DB_USER;"
sudo -u postgres psql -d $DB_NAME -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO $DB_USER;"
sudo -u postgres psql -d $DB_NAME -c "ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO $DB_USER;"

# Configure scram-sha-256 authentication
echo "Configuring PostgreSQL to use scram-sha-256 and allow remote connections..."

# Ensure password_encryption is scram-sha-256
PG_CONF="/etc/postgresql/$PG_VERSION/main/postgresql.conf"
sudo sed -i "s/#password_encryption = md5/password_encryption = scram-sha-256/" $PG_CONF

# Edit postgresql.conf to listen on all addresses
sudo sed -i "s/#listen_addresses = 'localhost'/listen_addresses = '$LISTEN_ADDRESS'/" $PG_CONF

# Edit pg_hba.conf to allow remote connections with scram-sha-256
PG_HBA="/etc/postgresql/$PG_VERSION/main/pg_hba.conf"
echo "host all all 0.0.0.0/0 scram-sha-256" | sudo tee -a $PG_HBA

# Restart PostgreSQL to apply changes
echo "Restarting PostgreSQL..."
sudo systemctl restart postgresql

# Test database connection
echo "Testing connection to database $DB_NAME with user $DB_USER..."
PGPASSWORD=$DB_PASSWORD psql -h localhost -U $DB_USER -d $DB_NAME -c "\l" >/dev/null 2>&1

if [ $? -eq 0 ]; then
    echo "Connection successful! PostgreSQL has been configured."
else
    echo "Error: Could not connect. Please check the configuration."
    exit 1
fi

echo "Complete! PostgreSQL has been installed and configured with database '$DB_NAME', user '$DB_USER', and remote connections enabled with scram-sha-256."
echo "You may need to configure the access control rules for accepting remote connections in firewalls and security groups."
echo "Now you can run the following command to create the database and user:"
echo "npx sequelize-cli db:create"
echo "npx sequelize-cli db:migrate"
echo "npx sequelize-cli db:seed:all"