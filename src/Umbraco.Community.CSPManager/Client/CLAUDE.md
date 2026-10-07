# CSP Manager - Frontend Client

## Build Commands

- `npm run build:ts` - TypeScript checking only (fast, use during development)
- `npm run build` - Full build (TypeScript + Vite bundling)
- `npm run dev` - Dev server with hot reload
- `npm run generate-client` - Regenerate OpenAPI client from backend swagger

## Testing

- API setup/teardown in specs goes through `CspApiHelpers(page.request)`: cookie auth plus the
  `Authorization: Bearer [redacted]` placeholder header the backoffice sends (the testhelpers'
  `umbracoApi` fixture expects a localStorage token that Umbraco 17+ doesn't have)

- `npm test` - Run Playwright E2E tests
- `npm run test:ui` - Playwright UI mode for debugging
- `npm run test:headed` - See browser during tests
- Custom test ID attribute: `data-mark`

## Architecture

- Lit 3.x web components extending `UmbLitElement`
- Manifest-based extension registration (all manifests aggregated in `bundle.manifests.ts`)
- Observable state via `UmbObjectState` with `observe()` subscriptions
- Repository pattern: repositories handle API calls, contexts manage state
- `src/api/` is auto-generated from OpenAPI spec - do not edit manually
- Domain policies (children of the Frontend tree node) share the `csp-policy` entity type and
  workspace with the global policies; `isGlobalPolicyId()` tells them apart. "Add Domain Policy"
  (Frontend only, `Umb.Condition.Entity.Unique`) opens `create/{domainKey}`: an unsaved draft copied
  from Frontend, posted with the empty id so the server assigns it. Domain/content names are user
  content - text bindings only, never `unsafeHTML`.

## Umbraco v17 Patterns

### Context Tokens - always use `UmbContextToken` with two parameters:

```typescript
new UmbContextToken<MyContext>('ContextAlias', 'unique.api.alias')
```

- Workspace contexts use `'UmbWorkspaceContext'` as the context alias
- Import `UmbContextToken` from `@umbraco-cms/backoffice/context-api`
- Import `UmbContextBase` from `@umbraco-cms/backoffice/class-api`
- Import `UmbRepositoryBase` from `@umbraco-cms/backoffice/repository`

### HTTP Client - must include credentials for cookie auth:

```typescript
credentials: 'include'  // Required for v17 authentication
```

## Key Files

- `src/constants.ts` - Package aliases, policy type UUIDs
- `src/workspace/context/workspace.context.ts` - Main workspace state and logic
- `vite.config.ts` - Build config, output to `wwwroot/App_Plugins/UmbracoCommunityCSPManager/`
