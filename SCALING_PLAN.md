# Manifest Scaling Plan

This roadmap describes how to evolve Manifest from a local-first ASP.NET Core and SQLite application into a small public SaaS that can support thousands of registered users. The plan keeps the current product shape intact while moving shared state, expensive work, and production operations onto services that can be scaled and recovered independently.

The assumed deployment model is a Dockerized ASP.NET Core web app on a VPS, managed PostgreSQL for durable data, Redis for shared ephemeral state, S3-compatible object storage for images and uploads, and a separate worker process built from the same codebase. The frontend should be split incrementally from the existing `ui.html`; this plan does not require a full SPA rewrite.

## Target Architecture

- Keep the ASP.NET Core web app as the main API and UI server.
- Replace SQLite with PostgreSQL for all shared mutable production data.
- Use Redis for rate-limit counters, short-lived locks, job coordination, and optional short-lived response cache entries.
- Store card images, failed scan debug images when enabled, and future user-uploaded assets in S3-compatible object storage.
- Add a background worker process from the same codebase for OCR, image fetches, mail sends, catalog refreshes, and price refreshes.
- Run local production-like development through Docker Compose with app, worker, Postgres, Redis, and an object storage emulator.
- Run the VPS deployment as app and worker containers behind Caddy or nginx with HTTPS.

The production system must not depend on local app filesystem state for user data. Any number of web containers should be able to serve traffic against the same PostgreSQL, Redis, and object storage services.

## Phase 1: Stabilize Current App

Add the production foundation before replacing storage or splitting major workflows.

- Add a production configuration layer using environment variables for database, Redis, object storage, worker mode, public URL, SMTP, and upload limits.
- Keep local SQLite mode temporarily for development and migration safety.
- Add `MANIFEST_ENV=Development|Production`.
- Require secure production defaults when `MANIFEST_ENV=Production`, including explicit configured secrets, HTTPS public URL, secure cookies, bounded upload sizes, and no development-only fallbacks.
- Add health endpoints:
  - `/api/health/live` returns process liveness.
  - `/api/health/ready` checks database, Redis, object storage, and worker queue connectivity.
- Add structured logging with request IDs.
- Include request route, duration, status code, and safe user/account identifiers where available.
- Add a central `IClock` or equivalent time abstraction so expiry, token, session, and throttling logic can be tested deterministically.

## Phase 2: Database Migration to PostgreSQL

Introduce storage boundaries first, then add PostgreSQL behind those boundaries.

- Add repository interfaces before changing storage behavior:
  - `ICardRepository`
  - `IUserRepository`
  - `IAccessRepository`
  - `IDeckRepository`
  - `ISessionRepository`
  - `IScanRepository`
- Move SQL schema out of `Database.Schema` into versioned migrations.
- Use a migration tool such as FluentMigrator or DbUp.
- Add PostgreSQL tables equivalent to the current SQLite tables:
  - `catalog`
  - `users`
  - `access_requests`
  - `sessions`
  - `collection`
  - `scan_log`
  - `prices`
  - `decks`
  - `deck_cards`
- Preserve current ownership semantics during migration. Existing unclaimed user `0` data should migrate either to the first real account or to an explicit owner account chosen by the migration command.
- Add production indexes:
  - `collection(user_id, card_id)`
  - `collection(user_id, updated_at DESC)`
  - `decks(user_id, updated_at DESC)`
  - `deck_cards(deck_id, card_id)`
  - `sessions(token_hash)`
  - `sessions(user_id)`
  - `access_requests(status, id DESC)`
  - `access_requests(email, id DESC)`
  - `catalog(card_id)`
  - `catalog(base_id)`
  - `catalog(set_label)`
  - trigram or full-text index for card name and type search.
- Use transactions with row-level locking or atomic `INSERT ... ON CONFLICT` statements to preserve current concurrent logging behavior.
- Add a one-way migration command:

```bash
manifest migrate-sqlite --sqlite ./manifest.db --postgres "$DATABASE_URL"
```

- Keep SQLite support only as legacy/local mode until PostgreSQL behavior is verified by tests and production-like staging.

## Phase 3: Storage and Image Pipeline

Move image state out of the application filesystem and make image fetches asynchronous.

- Replace local `img-cache/` as the source of truth with S3-compatible object storage.
- Store object keys as deterministic paths, for example `cards/{card_id}.png`.
- Keep local disk only as an optional ephemeral cache.
- Change `/img/{cardId}` to:
  - validate the card ID,
  - check object storage,
  - return the cached object or redirect to a signed URL if supported,
  - enqueue image fetch work if the object is missing,
  - return a placeholder or 404 without blocking on upstream fetch.
- Move official-site image downloads out of request handling.
- Add image metadata columns or a dedicated table with:
  - `card_id`
  - `status`
  - `source_url`
  - `object_key`
  - `last_attempt_at`
  - `failure_count`
  - `last_error`
- Add retry and backoff behavior for failed image fetches.

## Phase 4: Background Jobs

Add a worker mode that runs outside the request path and can be scaled separately.

- Add a worker executable mode, for example:

```bash
manifest worker
```

- Use PostgreSQL-backed jobs initially, or a Redis queue if that better fits the VPS deployment.
- Define job types:
  - `FetchCardImage`
  - `RunOcrScan`
  - `SendEmail`
  - `RefreshCatalog`
  - `RefreshPrices`
  - `PurgeExpiredSessions`
  - `PurgeExpiredAccessTokens`
- Make every job idempotent.
- Include correlation IDs on jobs and propagate them into logs.
- Make web requests enqueue work and return quickly.
- Convert OCR to an asynchronous flow:
  - `POST /api/scan` creates a scan job and returns `scan_id`.
  - `GET /api/scan/{scan_id}` returns `pending`, `complete`, or `failed`.
- For the first public launch, allow synchronous scan fallback behind a configuration flag, but default production to asynchronous jobs.

## Phase 5: Public Auth, Abuse Protection, and Sessions

Move auth-related state to shared services and harden public routes.

- Move login throttling from in-memory dictionaries to Redis.
- Rate-limit by IP, account name, and route family.
- Add global request size limits and stricter per-route limits for uploads and scan endpoints.
- Store sessions in PostgreSQL or Redis so session state is shared across app instances.
- Keep the current hashed session token design.
- Add CSRF protection for cookie-authenticated state-changing routes.
- Require production cookies to be `Secure`, `HttpOnly`, and `SameSite=Lax` or stricter.
- Add a full Content Security Policy after inline scripts and inline event handlers are removed.
- Add email verification for public registration and access requests.
- Keep owner/admin flows, but add explicit roles:
  - `owner`
  - `admin`
  - `user`
- Add audit log entries for account creation, login failures, access approvals, deck deletion, collection reset, and admin actions.

## Phase 6: API Pagination and Query Scaling

Replace unbounded responses with paginated APIs and move expensive filtering to the server.

- Replace large list responses with cursor-paginated APIs.
- Use this request/response shape:
  - `limit`
  - `cursor`
  - `next_cursor`
  - `items`
- Update `/api/search` to support cursor pagination and server-side sorting.
- Update `/api/collection` to return one page of cards.
- Return aggregate collection stats separately.
- Add `/api/collection/stats` for totals and set breakdown.
- Add pagination to `/api/decks`.
- Keep compatibility temporarily by allowing old calls with capped defaults.
- Move "not owned" and leader-color filtering from client-only logic to server-side query parameters.
- Add query timeout protection.
- Add minimum search length rules where needed to avoid expensive broad scans.

## Phase 7: Frontend Maintainability

Split the frontend incrementally without changing the product into a framework app.

- Keep the current UI and behavior during the first split.
- Split `ui.html` into:
  - static HTML shell,
  - `wwwroot/css/app.css`,
  - `wwwroot/js/api.js`,
  - `wwwroot/js/cards.js`,
  - `wwwroot/js/collection.js`,
  - `wwwroot/js/decks.js`,
  - `wwwroot/js/scan.js`,
  - `wwwroot/js/session.js`.
- Remove inline event handlers so strict CSP can be enabled.
- Update UI code to consume paginated APIs.
- Add loading, empty, error, and retry states for paginated data.
- Add loading, failed, complete, and retry states for asynchronous scan jobs.
- Avoid adding a frontend framework in this phase.

## Phase 8: Deployment, Observability, and Backups

Add deployment assets and operational safety nets before inviting public traffic.

- Add a `Dockerfile` for app and worker images.
- Add `docker-compose.yml` for local production-like development.
- Add `docker-compose.prod.yml` as a VPS deployment example.
- Include these services:
  - app
  - worker
  - Postgres or an external managed Postgres connection
  - Redis
  - Caddy or nginx
- Add an environment template at `.env.example`.
- Add a database backup script using `pg_dump`.
- Add object storage backup or sync guidance.
- Log request IDs, safe user IDs, route, duration, status, and job IDs.
- Add metrics for:
  - request count, duration, and errors,
  - database query duration,
  - queue depth,
  - job success and failure,
  - scan latency,
  - image fetch failures,
  - login and rate-limit events.
- Add alert thresholds for database failures, queue backlog, repeated job failures, and disk pressure.

## Phase 9: Testing and Load Validation

Expand test coverage around storage behavior, concurrency, public security, and the new worker model.

- Preserve existing behavior tests.
- Add repository contract tests that run against SQLite legacy mode and PostgreSQL mode until SQLite is retired.
- Add migration tests from a sample SQLite database.
- Add concurrency tests for:
  - simultaneous card increments,
  - bulk logging,
  - deck edits,
  - invite spending,
  - session expiry.
- Add API pagination tests for:
  - stable cursor order,
  - no duplicates,
  - no skipped rows,
  - deleted or updated rows during pagination.
- Add worker tests for:
  - job idempotency,
  - retry and backoff,
  - poison job handling,
  - async scan completion.
- Add security tests for:
  - CSRF rejection,
  - rate-limit enforcement,
  - secure cookie behavior,
  - owner/admin authorization boundaries.
- Add load tests with k6 or NBomber for:
  - search browsing,
  - collection logging,
  - deck editing,
  - image loads,
  - scan job submission.
- Define the public-launch load target:
  - 1,000 registered users,
  - 100 concurrent active users,
  - p95 API latency under 300 ms for normal reads,
  - p95 write latency under 500 ms excluding background jobs,
  - no lost increments under concurrent writes.

## Rollout Order

1. Add production config, health checks, and structured logs.
2. Introduce repository interfaces without changing behavior.
3. Add PostgreSQL migrations and dual repository implementations.
4. Add the SQLite-to-Postgres migration command.
5. Move sessions and rate limits to shared storage.
6. Add pagination and update frontend consumers.
7. Move image fetching to object storage and background jobs.
8. Move mail, OCR, catalog refresh, and price refresh to worker jobs.
9. Split frontend assets and enable strict CSP.
10. Add Docker/VPS deployment assets.
11. Add load tests, backups, metrics, and launch checklist.
12. Retire SQLite from production mode after migration is proven. (Done: Production
    refuses to start without a PostgreSQL `MANIFEST_DATABASE_URL`.)

## Acceptance Criteria

The Markdown plan is complete when it clearly states:

- The target public architecture.
- The concrete infrastructure choices.
- The phased implementation order.
- The database migration strategy.
- The API behavior changes.
- The background job model.
- The frontend refactor scope.
- The deployment and backup model.
- The test, load, and security acceptance criteria.

The SaaS implementation is complete when:

- The app can run multiple web containers against shared PostgreSQL, Redis, and object storage.
- No user data depends on local app filesystem state.
- Restarting one app instance does not clear sessions, throttles, or queued work.
- High-cost work is handled by workers.
- Large lists are paginated.
- Public traffic has rate limits, CSRF protection, secure cookies, and production CSP.
- Backups and restore documentation exist.
- Load tests meet the small-SaaS target.
