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
- Domain policies: rows with `DomainKey` override frontend policy on matching `PublishedRequest.Domain`. Disabled policies follow `DisabledDomainPolicyBehavior`. Backoffice always uses backoffice policy.
- Domain keys: `CspDomainKey.FromDomainName` derives key from hostname string (Umbraco domains lack persistent IDs). Renaming/deleting a hostname orphans its policy without deleting it; `MoveDomainPolicyAsync` re-assigns an orphan to another domain.
- Identity & locking: `EnsureValidIdentityAsync` enforces one policy per domain, assigns GUIDs if empty, and rejects domain keys on global IDs. Writes take `scope.EagerWriteLock(Constants.Locks.Definitions)` (`-2776`) to prevent SQLite deadlocks.
- Header validation: `CspDefinitionValidator` validates sources, directives, and reporting URIs across API, service, and uSync. Whitespace sources are pruned; invalid stored tokens are skipped during response generation.
- Caching: Domain policies cached per domain (`csp-domain-{key}`) with negative caching. Deletions publish `CspDeletedNotification` post-commit and trigger distributed cache refresh.
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
- `POST /csp/api/v1.0/Definitions/{id}/move?domainKey={key}` - move an orphaned domain policy to a
  domain without one; re-created under a new id (delete + save notifications), 400 if not orphaned
- `GET /csp/api/v1.0/Domains` - non-wildcard domains with culture, content node and policy status
  (`CspDomainsController`: Umbraco already has a `DomainsController`, and names must be unique)

## Configuration

- `CspManagerOptions.DisableBackOfficeHeader` - disable CSP on backoffice
- `CspManagerOptions.DisabledDomainPolicyBehavior` - `FallbackToGlobal` (default) or `NoHeader` for a
  disabled domain policy
- All CSP directives defined in `Constants.cs`
