# scripts/

Deploy tooling for Kotirauha. The app runs on the shared Hetzner VPS managed
by [Exovento](../../Exovento); host Caddy terminates TLS and routes
`kotirauha.xyz` to this app's containers.

## One-time server registration (done via Exovento)

```bash
cd /c/myGit/Exovento
./scripts/host-ssh.sh add-app kotirauha kotirauha.xyz armanatory/Kotirauha , no
```

This created `/srv/kotirauha` (empty), `/var/kotirauha/{pgdata,media}`,
`/srv/kotirauha/.env` (random secrets + ports), and a placeholder Caddy
snippet. Kotirauha's loopback ports: backend `5700`, frontend `5701`.

**DNS:** add an `A` record `kotirauha.xyz → 204.168.171.29` (and a
`CAA kotirauha.xyz 0 issue "letsencrypt.org"`) before the first deploy so
Caddy can issue the TLS certificate.

## Deploy from this laptop

1. `cp .env.deploy.example .env.deploy` and fill in `SSH_HOST`, `SSH_KEY`,
   `GHCR_PAT`, and the provider secrets (OpenAI / Mailjet).
2. `docker login ghcr.io` (PAT with `write:packages`).
3. Run:
   ```bash
   bash scripts/deploy-local.sh
   ```

`deploy-local.sh` builds the backend + frontend images, pushes them to GHCR,
then SSHes in and runs `hetzner-deploy.sh`, which:

- clones/refreshes `/srv/kotirauha` from `origin/main` (first deploy clones;
  later deploys `git reset --hard`),
- syncs the forwarded secrets into `/srv/kotirauha/.env`,
- pulls the images and rolls the compose stack,
- re-applies the Postgres password and force-recreates the backend,
- installs `Caddyfile.snippet` → `/etc/caddy/Caddyfile.d/kotirauha.caddyfile`
  and reloads Caddy.

`.env` on the server is gitignored, so `git reset --hard` never clobbers it.

## After the first deploy

The owner (`armanatory@gmail.com`) is a platform admin automatically. Sign in
with the magic link, and the `/admin` console is available from the account menu.
