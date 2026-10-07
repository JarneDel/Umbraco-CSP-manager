# CSP Manager - uSync Integration (Base)

Provides uSync serialization and sync support for CSP Manager configurations.

## Components

- **Serializers/CspDefinitionSerializer.cs** - CSP definition to/from XML serialization
- **Handlers/CspDefinitionHandler.cs** - `SyncHandlerRoot<CspDefinition>`, exports on `CspSavedNotification`,
  writes a delete marker on `CspDeletedNotification`
- **Trackers/CspDefinitionTracker.cs** - Tracks changes to CSP properties for diff detection
- **CspItemNames.cs** - aliases/names: `backoffice`, `front-end`, `domain-{domainKey}`
- **Composer.cs** - Registers notification handlers

## Conditional References

- Default: `ProjectReference` to CSPManager (instant change flow during development)
- NuGet pack: `dotnet pack -p:UseProjectReferences=false` switches to `PackageReference`
- Version range: `$(CspManagerDependencyRange)`, defined in `src/Directory.Build.props`
  (default `[18.0.0, 19.0.0)` — accepts any 18.x). The release workflow overrides it
  for prerelease builds; bump `CspManagerDependencyFloor`, not this csproj, on a breaking change.

## Key Behaviors

- Serializes: Enabled, ReportOnly, ReportUri, ReportingDirective, UpgradeInsecureRequests, Sources,
  and `DomainKey` for domain policies only (global files are unchanged)
- Returns the backoffice and frontend definitions plus every domain policy
- uSync group: "Settings"
- Domain policies: created on import with the source id (CspService checks the domain exists, so
  import domains first); if the target already has a policy for that domain under another id, that
  one is updated; a file that would move an existing definition to another domain fails
- Invalid files: `DeserializeCoreAsync` runs `CspDefinitionValidator` and fails the item with the reason
  ("Invalid CSP definition: ...") instead of letting the service throw on save
- Deletes: the global definitions are never deleted; a domain policy delete marker deletes it on import
- Tests: `src/uSync/Umbraco.Community.CSPManager.uSync.Tests` (serializer, fake `ICspService`)
