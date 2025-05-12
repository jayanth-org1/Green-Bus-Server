#!/bin/bash

# Database connection details
DB_HOST=localhost
DB_NAME=transport_booking
DB_USER=postgres
DB_PASSWORD=root

# Run the migration script
PGPASSWORD=$DB_PASSWORD psql -h $DB_HOST -U $DB_USER -d $DB_NAME -f Scripts/001_CreateUsersTable.sql 