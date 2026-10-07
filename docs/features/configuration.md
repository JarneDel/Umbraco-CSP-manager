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

What a request gets when it's routed through a domain whose [domain policy](domain-policies) is disabled.

| Value | Behaviour |
|---|---|
| `FallbackToGlobal` | The Frontend policy applies, as if the domain had no policy. Disabling a domain policy never removes the protection the rest of the site has. |
| `NoHeader` | No CSP header is sent for that domain, the same way a disabled Frontend policy sends none. |

{: .warning }
`NoHeader` is an explicit fail-open opt-in: disabling a domain policy then removes all CSP protection from that domain. Keep the default unless you need a domain to run without a policy.

```json
{
  "CspManager": {
    "DisabledDomainPolicyBehavior": "NoHeader"
  }
}
```

A domain *without* a domain policy always uses the Frontend policy; this option only applies when a domain policy exists and is disabled.
