#!/bin/bash
# Run bot in Docker production environment
# Slash commands register globally (no DevGuildId)
#
# Usage:
#   ./run-bot-prod.sh [command] [flags]
#
# Commands:
#   start   (default)  Start the production environment
#   stop               Stop the production environment
#   status             Show container status
#   logs               Show live logs (Ctrl+C to exit)
#   update             Rebuild image and force-recreate bot (zero-downtime for DB)
#
# Flags:
#   --bundled-db       Use local postgres container (default is external DB)
#   --build            Force rebuild the bot image (with start command)
#   --external-db      No-op, kept for backwards compatibility
#   --stop             Legacy alias for 'stop' command
#   --logs             Legacy alias for 'logs' command

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$PROJECT_ROOT"

# --- Parse arguments ---
COMMAND=""
BUILD_FLAG=""
BUNDLED_DB=false

for arg in "$@"; do
    case $arg in
        start|stop|status|logs|update)
            COMMAND="$arg"
            ;;
        --bundled-db)
            BUNDLED_DB=true
            ;;
        --build)
            BUILD_FLAG="--build"
            ;;
        --external-db)
            echo "Note: --external-db is now the default and has no effect."
            ;;
        --stop)
            # Legacy flag support
            COMMAND="stop"
            ;;
        --logs)
            # Legacy flag support
            COMMAND="logs"
            ;;
        --help|-h)
            sed -n '2,/^$/p' "$0" | sed 's/^# \?//'
            exit 0
            ;;
    esac
done

# Default command is start
COMMAND="${COMMAND:-start}"

# --- Compose file selection ---
if [ "$BUNDLED_DB" = true ]; then
    COMPOSE_CMD="docker compose"
else
    COMPOSE_CMD="docker compose -f docker-compose.prod-external.yml"
fi

# --- Preflight checks ---
preflight() {
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

    # Only check default password when using bundled DB
    if [ "$BUNDLED_DB" = true ]; then
        if grep -q "CHANGE_THIS_PASSWORD" .env.production 2>/dev/null; then
            echo "Error: Change the default database password in .env.production"
            exit 1
        fi
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
}

# --- Commands ---

cmd_start() {
    preflight

    echo ""
    echo "=========================================="
    echo "Starting SevenThree (Production)"
    echo "=========================================="
    echo "Slash commands: Global registration"
    if [ "$BUNDLED_DB" = true ]; then
        echo "Database:       Bundled PostgreSQL"
    else
        echo "Database:       External"
    fi
    echo "=========================================="
    echo ""

    # Stop existing containers
    echo "Stopping existing containers..."
    $COMPOSE_CMD down 2>/dev/null || true

    if [ "$BUNDLED_DB" = true ]; then
        # Bundled DB mode: start postgres first, wait for healthy, then bot
        echo "Starting database..."
        $COMPOSE_CMD up -d postgres

        echo "Waiting for database to be healthy..."
        until $COMPOSE_CMD exec -T postgres pg_isready -U seventhree -d seventhree > /dev/null 2>&1; do
            echo "  Database not ready yet, waiting..."
            sleep 2
        done
        echo "Database is ready!"

        echo "Starting bot container..."
        $COMPOSE_CMD up -d $BUILD_FLAG bot
    else
        # External DB mode: start bot only
        echo "Using external database..."
        echo "Starting bot container..."
        $COMPOSE_CMD up -d $BUILD_FLAG bot
    fi

    echo ""
    echo "=========================================="
    echo "Production environment is running!"
    echo "=========================================="
    echo ""
    echo "Commands:"
    echo "  View logs:  ./run-bot-prod.sh logs"
    echo "  Status:     ./run-bot-prod.sh status"
    echo "  Stop:       ./run-bot-prod.sh stop"
    echo "  Update:     ./run-bot-prod.sh update"
    echo ""
    echo "Note: Global slash commands can take up to 1 hour to propagate."
    echo ""
}

cmd_stop() {
    preflight

    echo "Stopping production environment..."
    $COMPOSE_CMD down
    echo "Production environment stopped."
}

cmd_status() {
    preflight

    $COMPOSE_CMD ps
}

cmd_logs() {
    preflight

    echo "Showing logs (Ctrl+C to exit)..."
    $COMPOSE_CMD logs -f bot
}

cmd_update() {
    preflight

    echo "Rebuilding and restarting bot container..."
    $COMPOSE_CMD up -d --build --force-recreate bot
    echo "Bot container updated."
    echo ""
    $COMPOSE_CMD ps
}

# --- Dispatch ---
case "$COMMAND" in
    start)  cmd_start ;;
    stop)   cmd_stop ;;
    status) cmd_status ;;
    logs)   cmd_logs ;;
    update) cmd_update ;;
    *)
        echo "Unknown command: $COMMAND"
        echo "Run './run-bot-prod.sh --help' for usage."
        exit 1
        ;;
esac
