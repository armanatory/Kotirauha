#!/usr/bin/env bash
# Local fast-deploy for Kotirauha. Builds the backend + frontend images on
# this machine, pushes them to GHCR with a unique tag, then SSHes into the
# shared Hetzner VPS and runs /srv/kotirauha/scripts/hetzner-deploy.sh
# against that tag.
#
# One-time setup:
#   1. docker login ghcr.io          # PAT with write:packages
#   2. cp .env.deploy.example .env.deploy && edit values
#
# Usage:
#   ./scripts/deploy-local.sh              # build + push + deploy
#   DRY_RUN=1 ./scripts/deploy-local.sh    # build only
#   SKIP_DEPLOY=1 ./scripts/deploy-local.sh # build + push, no SSH
#
# Env (override via .env.deploy or shell):
#   SSH_HOST        e.g. deploy@204.168.171.29           (REQUIRED)
#   SSH_KEY         path to private key                  (default: ssh-agent)
#   SSH_PORT        default 22
#   IMAGE_OWNER     GitHub owner for GHCR images         (default: armanatory)
#   GHCR_PAT        token forwarded to the VPS for docker login + git fetch
#   TAG             custom image tag (default: local-<git-sha>)
#   Provider secrets forwarded into /srv/kotirauha/.env (optional):
#     OPENAI_API_KEY OPENAI_MODEL ANTHROPIC_API_KEY ANTHROPIC_MODEL
#     MAILJET_API_KEY MAILJET_API_SECRET MAILJET_FROM_EMAIL MAILJET_FROM_NAME
#     ADMIN_EMAIL GOOGLE_CLIENT_ID

set -euo pipefail
SECONDS=0

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

if [ -f .env.deploy ]; then
  set -a; # shellcheck disable=SC1091
  source .env.deploy; set +a
fi

: "${IMAGE_OWNER:=armanatory}"
: "${REGISTRY:=ghcr.io}"
: "${GHCR_USERNAME:=$IMAGE_OWNER}"
: "${SSH_PORT:=22}"

GIT_SHA="$(git rev-parse --short HEAD)"
GIT_BRANCH="$(git rev-parse --abbrev-ref HEAD)"
DIRTY=""
if ! git diff --quiet || ! git diff --cached --quiet; then DIRTY="-dirty"; fi
: "${TAG:=local-${GIT_SHA}${DIRTY}}"

IMG_BACKEND="${REGISTRY}/${IMAGE_OWNER}/kotirauha-backend:${TAG}"
IMG_FRONTEND="${REGISTRY}/${IMAGE_OWNER}/kotirauha-frontend:${TAG}"

echo "→ Tag    : $TAG"
echo "→ Branch : $GIT_BRANCH (${DIRTY:-clean})"
echo "→ Owner  : $IMAGE_OWNER"

# The server's hetzner-deploy.sh refreshes its scripts/compose/Caddyfile via
# `git reset --hard origin/main`, so unpushed local edits to those files won't
# take effect. Auto-push the branch so the server sees them.
UPSTREAM="$(git rev-parse --abbrev-ref --symbolic-full-name '@{upstream}' 2>/dev/null || true)"
if [ -n "$UPSTREAM" ]; then
  git fetch --quiet "${UPSTREAM%%/*}" "${UPSTREAM#*/}" 2>/dev/null || true
  AHEAD="$(git rev-list --count "${UPSTREAM}..HEAD" 2>/dev/null || echo 0)"
  if [ "$AHEAD" -gt 0 ]; then
    echo "→ pushing $AHEAD unpushed commit(s) so the server pulls current scripts/compose"
    git push "${UPSTREAM%%/*}" "$GIT_BRANCH"
  fi
fi

if [ -z "${SKIP_BUILD:-}" ]; then
  echo; echo "▶ Building backend image…"
  docker build -t "$IMG_BACKEND" -f docker/backend.Dockerfile .
  echo; echo "▶ Building frontend image…"
  docker build -t "$IMG_FRONTEND" -f docker/frontend.Dockerfile .
else
  echo "↷ SKIP_BUILD — using existing local images at $TAG"
fi

if [ -n "${DRY_RUN:-}" ]; then
  echo; echo "✓ Dry run complete (built, not pushed)."; exit 0
fi

# Authenticate to GHCR for the push. Uses the PAT from .env.deploy when present
# (the same account that owns the packages); otherwise relies on an existing
# `docker login ghcr.io`. The PAT must include the write:packages scope.
if [ -n "${GHCR_PAT:-}" ]; then
  echo; echo "▶ Logging in to ${REGISTRY} as ${GHCR_USERNAME}"
  echo "$GHCR_PAT" | docker login "$REGISTRY" -u "$GHCR_USERNAME" --password-stdin >/dev/null
fi

echo; echo "▶ Pushing images to ${REGISTRY}…"
docker push "$IMG_BACKEND"
docker push "$IMG_FRONTEND"

if [ -n "${SKIP_DEPLOY:-}" ]; then
  echo; echo "✓ Pushed but SKIP_DEPLOY set — server not touched."; exit 0
fi

: "${SSH_HOST:?Set SSH_HOST in .env.deploy (e.g. deploy@204.168.171.29)}"
SSH_OPTS=(-p "$SSH_PORT" -o StrictHostKeyChecking=accept-new -o BatchMode=yes)
[ -n "${SSH_KEY:-}" ] && SSH_OPTS+=(-i "$SSH_KEY")

# Forward provider secrets so the server's sync_env_key updates
# /srv/kotirauha/.env. Empty values are skipped (server keeps its value).
SSH_ENV_PAIRS=()
FORWARDED=()
add_forward() {
  local key="$1" val="$2"
  [ -z "$val" ] && return 0
  local escaped="${val//\'/\'\\\'\'}"
  SSH_ENV_PAIRS+=("${key}='${escaped}'")
  FORWARDED+=("$key")
  return 0
}
add_forward OPENAI_API_KEY     "${OPENAI_API_KEY:-}"
add_forward OPENAI_MODEL       "${OPENAI_MODEL:-}"
add_forward ANTHROPIC_API_KEY  "${ANTHROPIC_API_KEY:-}"
add_forward ANTHROPIC_MODEL    "${ANTHROPIC_MODEL:-}"
add_forward MAILJET_API_KEY    "${MAILJET_API_KEY:-}"
add_forward MAILJET_API_SECRET "${MAILJET_API_SECRET:-}"
add_forward MAILJET_FROM_EMAIL "${MAILJET_FROM_EMAIL:-}"
add_forward MAILJET_FROM_NAME  "${MAILJET_FROM_NAME:-}"
add_forward ADMIN_EMAIL        "${ADMIN_EMAIL:-}"
add_forward GOOGLE_CLIENT_ID   "${GOOGLE_CLIENT_ID:-}"

if [ "${#FORWARDED[@]}" -gt 0 ]; then
  echo "  → forwarding to .env on server: ${FORWARDED[*]}"
fi
SSH_ENV="${SSH_ENV_PAIRS[*]:-}"

# First-deploy bootstrap: add-app leaves /srv/kotirauha empty, and the deploy
# script lives in the repo — so clone/refresh it on the server before invoking
# it. Idempotent: subsequent deploys just fast-forward. .env is gitignored, so
# reset --hard never touches the server's secrets.
echo; echo "▶ Ensuring /srv/kotirauha checkout on $SSH_HOST"
ssh "${SSH_OPTS[@]}" "$SSH_HOST" \
  "IMAGE_OWNER='$IMAGE_OWNER' GHCR_USERNAME='$GHCR_USERNAME' GHCR_PAT='${GHCR_PAT:-}' bash -s" <<'REMOTE'
set -euo pipefail
cd /srv/kotirauha
REPO_URL="https://github.com/${IMAGE_OWNER:-armanatory}/Kotirauha.git"
GITARGS=(-c safe.directory=/srv/kotirauha)
if [ -n "${GHCR_PAT:-}" ]; then
  GITARGS+=(-c "url.https://${GHCR_USERNAME:-$IMAGE_OWNER}:${GHCR_PAT}@github.com/.insteadOf=https://github.com/")
fi
[ -d .git ] || git init -q
git remote get-url origin >/dev/null 2>&1 || git remote add origin "$REPO_URL"
git remote set-url origin "$REPO_URL"
GIT_TERMINAL_PROMPT=0 git "${GITARGS[@]}" fetch --quiet origin main
git "${GITARGS[@]}" reset --quiet --hard origin/main
chmod +x scripts/*.sh 2>/dev/null || true
echo "  ✓ /srv/kotirauha at $(git "${GITARGS[@]}" rev-parse --short HEAD)"
REMOTE

echo; echo "▶ Triggering hetzner-deploy.sh on $SSH_HOST"
ssh "${SSH_OPTS[@]}" "$SSH_HOST" \
  "TAG=$TAG IMAGE_OWNER=$IMAGE_OWNER GHCR_USERNAME=$GHCR_USERNAME GHCR_PAT='${GHCR_PAT:-}' $SSH_ENV \
   /srv/kotirauha/scripts/hetzner-deploy.sh"

SERVER_TAG="$(ssh "${SSH_OPTS[@]}" "$SSH_HOST" 'cat /srv/kotirauha/.last-deploy.tag 2>/dev/null' || echo '?')"
echo
echo "════════════════════════════════════════════════════════════"
echo "✓ Deploy finished in ${SECONDS}s — tag $TAG"
echo "  server .last-deploy.tag = $SERVER_TAG"
echo "════════════════════════════════════════════════════════════"
[ "$SERVER_TAG" = "$TAG" ] || { echo "⚠ server tag != pushed tag — investigate." >&2; exit 1; }
