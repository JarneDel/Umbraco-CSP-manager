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

- Umbraco 17+
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

[Domain policies](../features/domain-policies) sync with the global policies. Their files carry a `DomainKey` and are named after it, and deleting a domain policy writes a delete marker that deletes it on the next import.

| Situation on import | What happens |
|---|---|
| The policy doesn't exist yet | It's created with the same id as on the source environment |
| The target already has its own policy for that domain | That policy is updated; a domain never gets two |
| The domain doesn't exist on the target | The import fails for that policy. Import the domains (Content) first |
| A file would move an existing policy to another domain | The import fails for that policy |
| A file holds a value the backoffice would reject (e.g. a source with `;` or a line break, an unknown directive) | The import fails for that item with the reason; nothing is saved. Fix the file or the source environment's policy and export again |

{: .note }
A domain policy follows the domain *name*. It only applies on environments where the domain has the same hostname.
