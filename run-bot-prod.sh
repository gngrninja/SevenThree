#!/bin/bash
# Run bot in Docker production environment
# Slash commands register globally (no DevGuildId)
#
# Usage:
#   ./run-bot-prod.sh          # Start/restart the production environment
#   ./run-bot-prod.sh --build  # Force rebuild the bot image
#   ./run-bot-prod.sh --logs   # Show live logs
#   ./run-bot-prod.sh --stop   # Stop the production environment

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# Parse arguments
BUILD_FLAG=""
SHOW_LOGS=false
STOP_ONLY=false

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
    esac
done

# Check if .env.production exists
if [ ! -f "$PROJECT_ROOT/.env.production" ]; then
    echo "Error: .env.production not found!"
    echo "Copy .env.production.example to .env.production and fill in your values"
    exit 1
fi

# Verify critical vars are set
if grep -q "SEVENTHREE_BOT_TOKEN=your_discord_bot_token_here" .env.production 2>/dev/null; then
    echo "Error: Set your Discord token in .env.production"
    exit 1
fi

if grep -q "CHANGE_THIS_PASSWORD" .env.production 2>/dev/null; then
    echo "Error: Change the default database password in .env.production"
    exit 1
fi

# Warn if DevGuildId is set (commands won't register globally)
if grep -q "^SEVENTHREE_DevGuildId" .env.production 2>/dev/null; then
    echo "Warning: SEVENTHREE_DevGuildId is set in .env.production"
    echo "Slash commands will only register to that guild, not globally."
    echo "Remove it for production use."
    echo ""
fi

# Load environment variables (for POSTGRES_PASSWORD used by docker-compose)
export $(grep -v '^#' .env.production | grep -v '^$' | xargs)

# Handle stop command
if [ "$STOP_ONLY" = true ]; then
    echo "Stopping production environment..."
    docker compose down
    echo "Production environment stopped."
    exit 0
fi

echo ""
echo "=========================================="
echo "Starting SevenThree (Production)"
echo "=========================================="
echo "Slash commands: Global registration"
echo "=========================================="
echo ""

# Stop existing containers
echo "Stopping existing containers..."
docker compose down 2>/dev/null || true

# Step 1: Start ONLY the database first
echo "Starting database..."
docker compose up -d postgres

# Step 2: Wait for database to be healthy
echo "Waiting for database to be healthy..."
until docker compose exec -T postgres pg_isready -U seventhree -d seventhree > /dev/null 2>&1; do
    echo "  Database not ready yet, waiting..."
    sleep 2
done
echo "Database is ready!"

# Step 3: Start the bot container (migrations applied automatically on startup)
echo "Starting bot container..."
docker compose up -d $BUILD_FLAG bot

echo ""
echo "=========================================="
echo "Production environment is running!"
echo "=========================================="
echo ""
echo "Services:"
echo "  - Bot:      Running in Docker"
echo "  - Database: PostgreSQL"
echo ""
echo "Commands:"
echo "  View logs:  docker compose logs -f bot"
echo "  Stop:       ./run-bot-prod.sh --stop"
echo "  Rebuild:    ./run-bot-prod.sh --build"
echo ""
echo "Note: Global slash commands can take up to 1 hour to propagate."
echo ""

# Show logs if requested
if [ "$SHOW_LOGS" = true ]; then
    echo "Showing logs (Ctrl+C to exit)..."
    docker compose logs -f bot
fi
