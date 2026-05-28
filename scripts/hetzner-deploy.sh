#!/usr/bin/env bash
# Runs on the shared Hetzner VPS (invoked over SSH by deploy-local.sh).
# Refreshes /srv/kotirauha from origin/main, syncs forwarded secrets into
# .env, pulls the new images, rolls the stack, and installs the Caddy snippet.
#
# Required env (from the SSH session):
#   TAG, IMAGE_OWNER
# Optional:
#   GHCR_PAT, GHCR_USERNAME, and any provider secrets to sync into .env.

set -euo pipefail

APP_DIR="/srv/kotirauha"
REPO="${REPO:-${IMAGE_OWNER:-armanatory}/Kotirauha}"
REPO_URL="https://github.com/${REPO}.git"

cd "$APP_DIR"

if [ ! -f .env ]; then
  echo "ERROR: $APP_DIR/.env missing. Run Exovento add-app first." >&2
  exit 1
fi

export GIT_TERMINAL_PROMPT=0
export TAG="${TAG:?TAG not set}"
export IMAGE_OWNER="${IMAGE_OWNER:?IMAGE_OWNER not set}"

# ── Refresh the checkout (clone-on-first-deploy, else fetch/reset) ──
# .env is gitignored, so reset --hard never touches it.
GIT_ARGS=(-c safe.directory="$APP_DIR")
if [ -n "${GHCR_PAT:-}" ]; then
  GHCR_USER="${GHCR_USERNAME:-$IMAGE_OWNER}"
  GIT_ARGS+=(-c "url.https://${GHCR_USER}:${GHCR_PAT}@github.com/.insteadOf=https://github.com/")
fi
if [ ! -d .git ]; then
  git init -q
fi
git remote get-url origin >/dev/null 2>&1 || git remote add origin "$REPO_URL"
git remote set-url origin "$REPO_URL"
rm -f Caddyfile docker-compose.prod.yml 2>/dev/null || true
git "${GIT_ARGS[@]}" fetch --quiet origin main
git "${GIT_ARGS[@]}" reset --quiet --hard origin/main
chmod +x scripts/*.sh 2>/dev/null || true

# Re-exec the freshly-pulled script so any fix in this file takes effect
# on the same deploy (bash reads the old inode otherwise).
if [ -z "${KOTIRAUHA_DEPLOY_REEXEC:-}" ]; then
  export KOTIRAUHA_DEPLOY_REEXEC=1
  exec "$APP_DIR/scripts/hetzner-deploy.sh"
fi

# Strip CRLF if .env was ever edited from Windows (trailing \r breaks keys).
if grep -q $'\r' .env 2>/dev/null; then
  echo "⚠ .env had CRLF — stripping"
  sed -i 's/\r$//' .env
fi

echo "$TAG" > .last-deploy.tag
date -u +%FT%TZ > .last-deploy.at

# ── Sync forwarded secrets into .env (only non-empty keys) ──
sync_env_key() {
  local key="$1" val="$2"
  [ -z "$val" ] && return 0
  val="${val%$'\r'}"
  if grep -q "^${key}=" .env; then
    awk -v k="$key" -v v="$val" '
      $0 ~ "^"k"=" { print k"="v; next } { print }
    ' .env > .env.tmp && mv .env.tmp .env
  else
    echo "${key}=${val}" >> .env
  fi
}
sed -i '/^TAG=/d' .env
sync_env_key OPENAI_API_KEY     "${OPENAI_API_KEY:-}"
sync_env_key OPENAI_MODEL       "${OPENAI_MODEL:-}"
sync_env_key ANTHROPIC_API_KEY  "${ANTHROPIC_API_KEY:-}"
sync_env_key ANTHROPIC_MODEL    "${ANTHROPIC_MODEL:-}"
sync_env_key MAILJET_API_KEY    "${MAILJET_API_KEY:-}"
sync_env_key MAILJET_API_SECRET "${MAILJET_API_SECRET:-}"
sync_env_key MAILJET_FROM_EMAIL "${MAILJET_FROM_EMAIL:-}"
sync_env_key MAILJET_FROM_NAME  "${MAILJET_FROM_NAME:-}"
sync_env_key ADMIN_EMAIL        "${ADMIN_EMAIL:-}"
sync_env_key GOOGLE_CLIENT_ID   "${GOOGLE_CLIENT_ID:-}"

# ── GHCR auth + pull + roll ──
if [ -n "${GHCR_PAT:-}" ]; then
  echo "$GHCR_PAT" | docker login ghcr.io -u "${GHCR_USERNAME:-$IMAGE_OWNER}" --password-stdin
fi

DEPLOY_TAG="$TAG"; DEPLOY_OWNER="$IMAGE_OWNER"
set -a; source .env; set +a
export TAG="$DEPLOY_TAG"; export IMAGE_OWNER="$DEPLOY_OWNER"

COMPOSE=(docker compose --env-file ./.env -f docker-compose.yml -f docker-compose.prod.yml)
echo "Deploying TAG=$TAG (owner=$IMAGE_OWNER)"
"${COMPOSE[@]}" pull
"${COMPOSE[@]}" up -d --remove-orphans

# Re-apply POSTGRES_PASSWORD to the running role (only takes on first init).
if [ -n "${POSTGRES_PASSWORD:-}" ]; then
  PG_USER="${POSTGRES_USER:-kotirauha}"
  for i in $(seq 1 10); do
    "${COMPOSE[@]}" exec -T postgres pg_isready -U "$PG_USER" >/dev/null 2>&1 && break
    sleep 1
  done
  PW_ESCAPED="$(printf '%s' "$POSTGRES_PASSWORD" | sed "s/'/''/g")"
  "${COMPOSE[@]}" exec -T postgres psql -U "$PG_USER" -v ON_ERROR_STOP=1 \
    -c "ALTER USER \"$PG_USER\" WITH PASSWORD '$PW_ESCAPED';" >/dev/null || true
fi

# Force-recreate backend so rotated provider keys actually load.
"${COMPOSE[@]}" up -d --force-recreate --no-deps backend
sleep 3

docker image prune -f --filter "until=168h" >/dev/null || true

# ── Health probe (backend /health on its loopback port) ──
PORT_BACKEND="${PORT_BACKEND:-5700}"
echo -n "Waiting for backend /health on 127.0.0.1:${PORT_BACKEND} "
HEALTHY=0
for i in $(seq 1 30); do
  if curl -fsS --max-time 2 "http://127.0.0.1:${PORT_BACKEND}/health" >/dev/null 2>&1; then
    HEALTHY=1; echo " ok (${i}s)"; break
  fi
  echo -n "."; sleep 1
done
if [ "$HEALTHY" != 1 ]; then
  echo " ✗ backend did not become healthy" >&2
  "${COMPOSE[@]}" logs --tail=80 --no-color backend >&2 || true
  exit 1
fi

# ── Install/refresh the Caddy snippet and reload ──
if [ -f Caddyfile.snippet ] && [ -d /etc/caddy/Caddyfile.d ]; then
  sudo install -m 0644 -o root -g root Caddyfile.snippet /etc/caddy/Caddyfile.d/kotirauha.caddyfile
  if sudo caddy validate --config /etc/caddy/Caddyfile >/dev/null 2>&1; then
    sudo systemctl reload caddy && echo "↻ Reloaded host Caddy"
  else
    echo "⚠ Caddyfile failed validate — snippet installed, NOT reloaded" >&2
    sudo caddy validate --config /etc/caddy/Caddyfile >&2 || true
  fi
fi

echo "Deploy complete: $TAG"
