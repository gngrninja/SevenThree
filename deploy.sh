#!/bin/bash
set -e

# Configuration from environment (set via /var/lib/jenkins/seventhree.env)
DEPLOY_DIR="${SEVENTHREE_DEPLOY_DIR}"
DEPLOY_USER="${SEVENTHREE_DEPLOY_USER}"
DEPLOY_HOST="${SEVENTHREE_DEPLOY_HOST}"

if [ -z "$DEPLOY_DIR" ] || [ -z "$DEPLOY_USER" ]; then
    echo "Error: DEPLOY_DIR or DEPLOY_USER is empty. Check /var/lib/jenkins/seventhree.env"
    exit 1
fi

if [ -z "$DEPLOY_HOST" ]; then
    echo "Error: DEPLOY_HOST is empty. Check /var/lib/jenkins/seventhree.env"
    exit 1
fi

SCRIPT_DIR="$(cd -- "$(dirname -- "$0")" && pwd)"
RSYNC_SRC="$SCRIPT_DIR/"
SSH_TARGET="${DEPLOY_USER}@${DEPLOY_HOST}"

# Run a command on the remote host as the deploy user
run_remote() {
    ssh -o StrictHostKeyChecking=accept-new "$SSH_TARGET" "$@"
}

echo "========================================="
echo "SevenThree Deployment"
echo "========================================="
echo "Deploying to: $SSH_TARGET:$DEPLOY_DIR"

# Sync code
echo "[1/3] Syncing code..."
run_remote "mkdir -p \"$DEPLOY_DIR\""
rsync -av --delete \
    --exclude='.git' \
    --exclude='TestResults' \
    --exclude='*.user' \
    --exclude='bin' \
    --exclude='obj' \
    --exclude='.nuget' \
    --exclude='.env*' \
    --exclude='logs' \
    --exclude='backups' \
    --exclude='src/SevenThree/import' \
    -e "ssh -o StrictHostKeyChecking=accept-new" \
    "$RSYNC_SRC" "$SSH_TARGET:$DEPLOY_DIR"/

# Build and restart (DB stays up, migrations run on app startup)
echo "[2/3] Building and restarting..."
run_remote "cd \"$DEPLOY_DIR\" && chmod +x ./run-bot-prod.sh && ./run-bot-prod.sh update"

# Verify
echo "[3/3] Checking status..."
run_remote "cd \"$DEPLOY_DIR\" && ./run-bot-prod.sh status"

echo ""
echo "Deployment complete."
