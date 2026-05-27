# Spec 01 — Project Setup

## Goal

Scaffold the full Kotirauha repository: React frontend, .NET 10 backend,
PostgreSQL database, Docker configuration, Caddy reverse proxy, and a GitHub
Actions CI pipeline.

At the end of this spec the project builds and runs locally with
`docker compose up --build`, and CI passes on push to `main`. No feature logic
yet — only the skeleton and the authenticated app shell.

---

## Folder structure to create

```
kotirauha/
├── frontend/
│   ├── src/
│   │   ├── components/
│   │   ├── pages/
│   │   ├── features/
│   │   ├── hooks/
│   │   ├── api/
│   │   ├── layouts/
│   │   ├── lib/
│   │   └── types/
│   ├── index.html
│   ├── vite.config.ts
│   ├── tsconfig.json
│   ├── package.json
│   └── .env.example
│
├── backend/
│   ├── Kotirauha.Api/
│   ├── Kotirauha.Core/
│   ├── Kotirauha.Infrastructure/
│   └── Kotirauha.Tests/
│
├── docker/
│   ├── frontend.Dockerfile
│   └── backend.Dockerfile
│
├── docs/                 # already exists
├── .github/workflows/ci.yml
├── docker-compose.yml
├── docker-compose.override.yml
├── Caddyfile
├── .env.example
├── .gitignore
├── CLAUDE.md             # already exists
└── README.md
```

---

## Frontend setup (`frontend/`)

### Technology
- React 18, Vite 5, TypeScript (strict mode), Tailwind CSS
- React Router v6, React Query (`@tanstack/react-query` v5)
- Axios wrapped in typed clients under `src/api/`
- Toasts: `sonner`
- i18n: `react-i18next` (resident UI must support multiple languages)

### `tsconfig.json`
`strict: true`, `@/*` → `src/*` path alias.

### `vite.config.ts`
- `@` alias → `src/`
- dev proxy `/api` → `http://localhost:5000`

### Layouts
- **`AuthLayout`** — `/login`, `/register`. Centered card, Kotirauha wordmark.
- **`AppLayout`** — all authenticated pages. Top bar with current building name +
  user menu (display name + logout). Phone-first: primary nav as a bottom bar on
  mobile, left sidebar on desktop.

### Route map (stubs in this spec)
```
/                       → LandingPage          (public)
/login                  → LoginPage            (AuthLayout, public)
/register               → RegisterPage         (AuthLayout, public)
/timeline               → TimelinePage         (AppLayout, private)   # home for residents
/entries/new            → NewEntryPage         (AppLayout, private)
/entries/:id            → EntryDetailPage      (AppLayout, private)
/building               → BuildingPage         (AppLayout, private)
/export                 → ExportPage           (AppLayout, private)
/profile                → ProfilePage          (AppLayout, private)
*                       → NotFoundPage         (AppLayout, private)
```
Private routes redirect to `/login` when unauthenticated.

### Placeholder pages
Each route is an empty stub with an `<h1>` of the page name; later specs fill them.

### `.env.example`
```
VITE_API_BASE_URL=http://localhost:5000
```

---

## Backend setup (`backend/`)

### Technology
- .NET 10 Web API, minimal API style
- EF Core 10 + Npgsql
- BCrypt.Net-Next, Microsoft.AspNetCore.Authentication.JwtBearer

### Projects
- **`Kotirauha.Api`** — `Program.cs`, middleware (HTTPS redirect, CORS, AuthN,
  AuthZ, ProblemDetails handler), empty route groups, `GET /health`.
- **`Kotirauha.Core`** — domain models, interfaces, value objects. No EF refs.
- **`Kotirauha.Infrastructure`** — `KotirauhaDbContext`, Npgsql connection from
  `DATABASE_URL`. No tables yet.
- **`Kotirauha.Tests`** — xUnit, one smoke test that `/health` returns 200.

### Migration auto-run on startup
```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KotirauhaDbContext>();
    db.Database.Migrate();
}
```

### Environment variables required
```
DATABASE_URL=Host=localhost;Database=kotirauha;Username=kotirauha;Password=kotirauha
JWT_SECRET=<minimum 32-char random string>
JWT_ISSUER=kotirauha
JWT_AUDIENCE=kotirauha-users
CORS_ORIGINS=http://localhost:5173
TRANSLATION_PROVIDER=anthropic
ANTHROPIC_API_KEY=<set in later spec>
```

---

## Docker & Caddy

### `docker/frontend.Dockerfile`
Stage 1 (Node 20): `npm ci`, `npm run build`. Stage 2: copy `dist/` (served by
Caddy in compose).

### `docker/backend.Dockerfile`
Stage 1 (.NET SDK): restore + publish. Stage 2 (.NET runtime): copy output,
expose 8080.

### `docker-compose.yml`
Services:
- `postgres` — PostgreSQL 16, internal only, named volume `pgdata`,
  healthcheck `pg_isready`
- `backend` — built from `docker/backend.Dockerfile`, depends on `postgres`
  healthy, healthcheck `curl -f http://localhost:8080/health`
- `frontend` — built static assets
- `caddy` — reverse proxy + TLS, only publicly exposed service, mounts `Caddyfile`

### `Caddyfile`
- serve frontend static files
- reverse-proxy `/api/*` → `backend:8080`
- automatic HTTPS in production; `localhost` for local

### `docker-compose.override.yml`
Local dev overrides (source mounts, hot reload).

---

## GitHub Actions CI (`.github/workflows/ci.yml`)

Trigger: `push` and `pull_request` on `main`.

- **build-backend**: setup .NET 10 → restore → build → `dotnet test` (with a
  Postgres service container)
- **build-frontend**: setup Node 20 → `npm ci` → `npm run type-check` →
  `npm run build`

---

## `.gitignore`
Ignore `.env`, `node_modules/`, `frontend/dist/`, `backend/**/bin`,
`backend/**/obj`, `*.user`, `.DS_Store`.

---

## Acceptance criteria

- [ ] `docker compose up --build` starts postgres, backend, frontend, caddy
- [ ] Backend waits for Postgres healthy before starting; migrations run on start
- [ ] `GET /health` returns `200 OK`
- [ ] Frontend landing page loads; private routes redirect to `/login`
- [ ] After a stubbed login the AppLayout renders with nav
- [ ] Toasts work; i18n scaffolding loads at least two locales (en, fi)
- [ ] `dotnet test` passes; `npm run build` passes with no TS errors
- [ ] CI passes on push to `main`

---

## Status
- [x] Scaffold shipped: .NET 10 API (`/health`, EF Core + Npgsql, migrate-on-start),
      React 19 + Vite + TS (strict) + Tailwind v4 app shell with Auth/App layouts,
      route guards, i18n (en/fi), React Query, sonner. Postgres runs in Docker.
      Verified with Playwright: landing renders, login renders, protected routes
      redirect to `/login`, no console errors. CI workflow + production Dockerfiles
      remain to be added.
