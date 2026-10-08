---
title: Configuration
parent: Features
nav_order: 2
---

# Configuration

CSP Manager can be configured via `appsettings.json` under the `CspManager` key.

```json
{
  "CspManager": {
    "DisableBackOfficeHeader": false,
    "DisabledDomainPolicyBehavior": "FallbackToGlobal"
  }
}
```

## Options

### DisableBackOfficeHeader

**Type**: `bool`
**Default**: `false`

Emergency kill switch to disable the CSP header for the Umbraco backoffice. When set to `true`, no `Content-Security-Policy` or `Content-Security-Policy-Report-Only` header is added to backoffice responses, regardless of what the backoffice policy is configured to do.

Use this if a misconfigured backoffice CSP policy locks you out of the Umbraco admin interface:

```json
{
  "CspManager": {
    "DisableBackOfficeHeader": true
  }
}
```

Remember to set it back to `false` once you have fixed the policy. See [Troubleshooting](../troubleshooting) for more on recovering from a broken backoffice CSP.

### DisabledDomainPolicyBehavior

**Type**: `FallbackToGlobal` \| `NoHeader`
**Default**: `FallbackToGlobal`

Controls which policy applies when a request matches a domain whose [domain policy](domain-policies) is disabled.

| Value | Behaviour |
|---|---|
| `FallbackToGlobal` | Applies the Frontend policy, as if the domain had no policy. Disabling a domain policy retains baseline site protection. |
| `NoHeader` | Sends no CSP header for that domain, matching the behaviour of a disabled Frontend policy. |

{: .warning }
`NoHeader` is an explicit fail-open setting: disabling a domain policy removes all CSP protection from that domain. Keep the default unless you specifically require an unconstrained domain.

```json
{
  "CspManager": {
    "DisabledDomainPolicyBehavior": "NoHeader"
  }
}
```

Domains without a domain policy always use the Frontend policy; this option only applies when a configured domain policy is disabled.
