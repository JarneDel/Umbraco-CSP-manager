---
title: Policy Settings
parent: User Guide
nav_order: 3
---

# Policy Settings

Each policy (frontend and backoffice) has settings that control how the CSP header is sent.

![Policy Settings section](../assets/images/screenshots/settings-screen.png "Policy Settings section"){: width="3840" height="2160" }

## Enabled

Toggles whether the CSP header is sent for this policy. When disabled, no `Content-Security-Policy` or `Content-Security-Policy-Report-Only` header is added to responses.

## Report Only

When enabled, the policy is sent as `Content-Security-Policy-Report-Only` instead of `Content-Security-Policy`. The browser reports violations (to the configured Report URI) but does not block any resources.

This is useful for:
- Testing a new policy before enforcing it
- Monitoring violations in production without breaking the site
- Diagnosing issues when resources are unexpectedly blocked

## Reporting Directive

Selects how violations are reported: **No reporting**, `report-to` (modern, requires a Reporting API endpoint) or `report-uri` (legacy, widely supported).

## Report URI

Shown when a reporting directive is selected. For `report-uri`, the URL the browser sends CSP violation reports to: an absolute `http`/`https` URL or a relative path. For `report-to`, the name of an endpoint from your `Reporting-Endpoints` header, such as `csp-endpoint` (letters, digits and ``!#$%&'*+-.^_`|~``).

## What a value can contain

Every source, directive and Report URI is written straight into the response header, so each must be a single value: no spaces, `;`, `,` or line breaks. A `;` would start a new directive and a line break makes the server drop the header entirely, so the save is rejected with a message naming the value. The same check applies however the policy is saved: the backoffice, a [uSync](../integrations/usync) import or your own code calling `ICspService`.

{: .note }
Policies saved before this check existed keep working: the header is built from them as before. If one holds a value that is no longer accepted, the next save asks you to fix it. A stored value with a line break is left out of the header (and logged) instead of losing the whole header.

## Upgrade Insecure Requests

When enabled, adds the `upgrade-insecure-requests` directive to the CSP header. This instructs browsers to automatically rewrite HTTP resource URLs to HTTPS before fetching them.

This is most useful when:
- Migrating a site from HTTP to HTTPS and legacy content still references HTTP URLs
- You want to enforce HTTPS loading of all resources without updating every URL manually

Note that `upgrade-insecure-requests` does not affect cross-origin requests — it only applies to same-origin and navigational requests for the current document's origin.
