# CSP Manager - Main Package

## Architecture

- **Controllers/**: API endpoints at `/csp/api/v1.0` with custom authorization
- **Services/**: `ICspService` - core business logic, nonce generation, caching
- **Middleware/**: `CspMiddleware` injects CSP headers via `Response.OnStarting()` callback
- **Models/**: `CspDefinition` (NPoco entity), `CspDefinitionSource`, API DTOs
- **Notifications/**: `CspSavedNotification`, `CspDeletedNotification`, `CspWritingNotification` for extensibility
- **TagHelpers/**: `CspNonceTagHelper` for `<script csp-manager-add-nonce>` and `<style>` tags

## Key Patterns

- Dual context: separate policies for frontend (`fac780be-...`) and backoffice (`9cbfa28c-...`)
- Domain policies: rows with `DomainKey` set replace the frontend policy for requests Umbraco routed
  through that domain (`PublishedRequest.Domain`, resolved inside `OnStarting`). Backoffice requests
  never use them. A disabled one follows `DisabledDomainPolicyBehavior`; a domain without one uses
  the frontend policy. Global queries filter `DomainKey IS NULL`.
- `DomainKey` is derived from the domain *name* (`CspDomainKey.FromDomainName`): Umbraco doesn't
  persist a key for domains (`IDomain.Key` is a new Guid on every load). The middleware derives it
  from `Domain.Name`, so there is no id-to-key map to invalidate. Renaming a domain orphans its
  policy (kept, no effect, deletable).
- The service owns identity (`EnsureValidIdentityAsync`, inside the save scope): global ids never
  carry a `DomainKey`; other ids must; an existing domain policy keeps its domain; `Guid.Empty` gets a
  server-assigned id; a new one needs an existing non-wildcard domain without a policy (also a
  filtered unique index); domain policies are never backoffice; sources always belong to the
  definition being saved. Violations throw `CspDefinitionValidationException` (400 from the API).
- Every write (save, create, delete) takes `scope.EagerWriteLock(Constants.Locks.Definitions)` (`-2776`,
  row added by `DefinitionsLockMigration`) as the first thing in its scope, before any read. Without it
  concurrent saves deadlock on SQLite (each holds a read snapshot and can't upgrade to a write). Do
  nothing slow or lock-taking while holding it: the `IDomainService` lookup runs before the scope. A
  unique-index violation on `DomainKey` (a writer bypassing the service) is still mapped to
  `CspDefinitionValidationException`.
- Header content rules (single-token sources, known directives, reporting directive/Report URI) live in
  `CspDefinitionValidator` (public, so uSync can use it). `CspApiDefinition.Validate` (model state, 400)
  and `SaveCspDefinitionAsync` (every caller) both apply it; whitespace-only sources are dropped first.
  Stored rows are not re-validated on read; the middleware skips any value with a control character.
- Domain policies are cached per domain (`csp-domain-{key}`) with the same Task/fault-eviction/clone
  pattern; a domain without a policy caches a completed `Task` with a `null` result (negative cache).
  `IAppCache.ClearByKey` matches by prefix, so `RefreshAll` clears them all with the prefix.
- Deletes publish `CspDeletedNotification` post-commit; the cache handler and the distributed
  refresher treat it like a save (the refresher payload type is `CspSavedNotification`).
- Cache-first retrieval with distributed cache invalidation on save
- Cache invalidation ordering is easy to regress: `GetCachedCspDefinitionAsync` caches the
  in-flight `Task` (not the awaited result) so a concurrent save can't overwrite an invalidation
  with a stale result; `SaveCspDefinitionAsync` publishes `CspSavedNotification` only after the
  scope disposes (post-commit); invalidation broadcasts to every server, not just the scheduling
  publisher, since a save can land on any of them.
- `GetCachedCspDefinitionAsync` returns a defensive copy (`CloneDefinition`) of the cached
  instance, never the cached reference itself - `CspWritingNotification` hands the result to
  consumer code, and the documented handler pattern mutates `CspDefinition.Sources` directly, so
  returning the shared reference would let one handler's mutation corrupt what every other
  request sharing the cache sees.
- Nonce-per-request: cryptographically secure, reused within HTTP context
- Nonce directive targeting: the nonce goes on every configured directive in the
  `script-src`/`script-src-elem` and `style-src`/`style-src-elem` pairs. Supporting browsers consult
  the `-elem` variant for `<script>`/`<style>`/`<link>` and ignore the broader one; older browsers only
  know the broader one, so both need it. A directive is never created just to hold a nonce (that would
  block every other source); if neither in a pair exists, `CspNonceDirectiveMissing` is logged.
  Inline event handlers/style attributes with `'unsafe-inline'` belong in `script-src-attr`/`style-src-attr`.
- Middleware never breaks requests on failure
- Composer pattern: `CspManagerComposer` auto-registers via `IComposer`

## API Endpoints

- `GET /csp/api/v1.0/Definitions?isBackOffice=false` - retrieve a global CSP definition
- `GET /csp/api/v1.0/Definitions?domainKey={key}` - retrieve a domain policy (404 if none)
- `GET /csp/api/v1.0/Definitions/{id}` - retrieve any definition by id (404 if none)
- `GET /csp/api/v1.0/Definitions/domain-policies` - list domain policies (name, orphaned flag)
- `POST /csp/api/v1.0/Definitions/save` - save a definition; a domain policy posted with
  `Guid.Empty` is created (server-assigned id), any other unknown domain-policy id is 404
- `POST /csp/api/v1.0/Definitions/create-from-frontend?domainKey={key}` - create a domain policy as a
  copy of the frontend policy
- `DELETE /csp/api/v1.0/Definitions/{id}` - delete a domain policy (400 for the global ones)
- `GET /csp/api/v1.0/Domains` - non-wildcard domains with culture, content node and policy status
  (`CspDomainsController`: Umbraco already has a `DomainsController`, and names must be unique)

## Configuration

- `CspManagerOptions.DisableBackOfficeHeader` - disable CSP on backoffice
- `CspManagerOptions.DisabledDomainPolicyBehavior` - `FallbackToGlobal` (default) or `NoHeader` for a
  disabled domain policy
- All CSP directives defined in `Constants.cs`
