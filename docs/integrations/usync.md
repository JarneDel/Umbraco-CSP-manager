---
title: uSync
parent: Integrations
nav_order: 1
---

# uSync Integration

[Umbraco.Community.CSPManager.uSync](https://www.nuget.org/packages/Umbraco.Community.CSPManager.uSync/) adds support for synchronizing CSP policies across Umbraco environments using [uSync](https://github.com/KevinJump/uSync).

Once installed, uSync automatically includes your CSP definitions in its export/import cycle — syncing CSP configurations alongside your other Umbraco settings.

![uSync synchronization of CSP definitions](../assets/images/screenshots/usync-screen.png "uSync synchronization"){: width="3840" height="2160" }

## Requirements

- Umbraco 18+
- [Umbraco.Community.CSPManager](https://www.nuget.org/packages/Umbraco.Community.CSPManager/) 17+
- [uSync.BackOffice](https://www.nuget.org/packages/uSync.BackOffice/) 17+

## Installation

```bash
dotnet add package Umbraco.Community.CSPManager.uSync
```

Or via the Package Manager Console:

```powershell
Install-Package Umbraco.Community.CSPManager.uSync
```

No further configuration is needed. After installation, CSP definitions appear in the uSync export/import cycle automatically.

## How It Works

The package registers a uSync serializer for `CspDefinition` objects. When uSync exports or imports settings, it includes the frontend and backoffice CSP policies. This means you can:

- Export CSP policies from one environment and import them into another via the uSync backoffice UI
- Include CSP policies in version-controlled uSync XML files
- Deploy CSP policy changes alongside code using your normal deployment pipeline

## Domain policies

[Domain policies](../features/domain-policies) sync alongside global policies. Exported files include the `DomainKey` in the filename (`csp.domain.{domainKey}.config`). Deleting a domain policy writes a delete marker so the removal syncs on the next import. Moving an orphaned policy to another domain exports as a delete of the old policy plus a new policy, so sync the renamed hostname (Culture and Hostnames) before the CSP policies.

| Import condition | Outcome |
|---|---|
| Policy does not exist on target | Created with the exported ID. |
| Target already has a policy for that domain | Existing policy is updated. |
| Domain does not exist on target | Import fails with an error. Import domains (Culture and Hostnames) before importing CSP policies. |
| File reassigns an existing policy ID to a different domain | Import fails with an error. |
| Policy contains invalid values (e.g. `;`, `,`, newlines, unknown directives) | Import fails with validation errors. |

{: .note }
Domain policies match the hostname string configured in Culture and Hostnames. They only apply on environments configured with the matching hostname.

