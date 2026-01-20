#!/bin/bash
# Run bot in Docker development environment (matches production setup)
# This runs both PostgreSQL and the bot in Docker containers
#
# Usage:
#   ./run-bot-dev.sh          # Start/restart the dev environment
#   ./run-bot-dev.sh --build  # Force rebuild the bot image
#   ./run-bot-dev.sh --logs   # Show live logs
#   ./run-bot-dev.sh --stop   # Stop the dev environment
#   ./run-bot-dev.sh --local  # Run bot locally (not in Docker) against Docker DB

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# Parse arguments
BUILD_FLAG=""
SHOW_LOGS=false
STOP_ONLY=false
RUN_LOCAL=false

for arg in "$@"; do
    case $arg in
        --build)
            BUILD_FLAG="--build"
            ;;
        --logs)
            SHOW_LOGS=true
            ;;
        --stop)
            STOP_ONLY=true
            ;;
        --local)
            RUN_LOCAL=true
            ;;
    esac
done

# Check if .env.development exists
if [ ! -f "$PROJECT_ROOT/.env.development" ]; then
    echo "Error: .env.development not found!"
    echo "Copy .env.development.example to .env.development and add your Discord token"
    exit 1
fi

# Verify critical vars are set
if grep -q "SEVENTHREE_BOT_TOKEN=your_discord_bot_token_here" .env.development 2>/dev/null; then
    echo "Error: Set your Discord token in .env.development"
    exit 1
fi

# Auto-create .env.development.docker if missing (needed for Docker networking)
if [ ! -f "$PROJECT_ROOT/.env.development.docker" ]; then
    echo "Creating .env.development.docker from example..."
    cp "$PROJECT_ROOT/.env.development.docker.example" "$PROJECT_ROOT/.env.development.docker"
fi

# Load environment variables
export $(grep -v '^#' .env.development | grep -v '^$' | xargs)

# Handle stop command
if [ "$STOP_ONLY" = true ]; then
    echo "Stopping dev environment..."
    docker compose -f docker-compose.dev.yml down
    echo "Dev environment stopped."
    exit 0
fi

# Handle local mode (bot runs outside Docker, DB in Docker)
if [ "$RUN_LOCAL" = true ]; then
    echo "Starting in LOCAL mode (bot outside Docker)..."

    # Start only the database
    if ! docker ps | grep -q seventhree-postgres-dev; then
        echo "Starting dev database..."
        docker compose -f docker-compose.dev.yml up -d postgres-dev
        echo "Waiting for database to be healthy..."
        sleep 5
    fi

    echo ""
    echo "=========================================="
    echo "Starting SevenThree (Local Mode)"
    echo "=========================================="
    echo "Bot: Running locally via dotnet run"
    echo "Database: Docker (localhost:5473)"
    echo "=========================================="
    echo ""
    echo "Migrations will be applied automatically on startup."
    echo ""

    cd src/SevenThree
    dotnet run
    exit 0
fi

# Docker mode (default)
echo ""
echo "=========================================="
echo "Starting SevenThree (Docker Mode)"
echo "=========================================="
echo "Bot: Docker container"
echo "Database: Docker container"
echo "=========================================="
echo ""

# Stop existing containers
echo "Stopping existing containers..."
docker compose -f docker-compose.dev.yml down 2>/dev/null || true

# Step 1: Start ONLY the database first
echo "Starting database..."
docker compose -f docker-compose.dev.yml up -d postgres-dev

# Step 2: Wait for database to be healthy
echo "Waiting for database to be healthy..."
until docker compose -f docker-compose.dev.yml exec -T postgres-dev pg_isready -U seventhree -d seventhree > /dev/null 2>&1; do
    echo "  Database not ready yet, waiting..."
    sleep 2
done
echo "Database is ready!"

# Step 3: Start the bot container (migrations applied automatically on startup)
echo "Starting bot container..."
docker compose -f docker-compose.dev.yml up -d $BUILD_FLAG seventhree-dev

echo ""
echo "=========================================="
echo "Dev environment is running!"
echo "=========================================="
echo ""
echo "Services:"
echo "  - Bot:      Running in Docker"
echo "  - Database: PostgreSQL (localhost:5473)"
echo ""
echo "Commands:"
echo "  View logs:  docker compose -f docker-compose.dev.yml logs -f seventhree-dev"
echo "  Stop:       ./run-bot-dev.sh --stop"
echo "  Rebuild:    ./run-bot-dev.sh --build"
echo ""

# Show logs if requested
if [ "$SHOW_LOGS" = true ]; then
    echo "Showing logs (Ctrl+C to exit)..."
    docker compose -f docker-compose.dev.yml logs -f seventhree-dev
fi
