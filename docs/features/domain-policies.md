---
title: Domain Policies
parent: Features
nav_order: 3
---

# Domain policies

Give a domain its own CSP. When a site has several domains under **Culture and Hostnames**, a domain policy replaces the global **Frontend** policy for requests Umbraco routes through that domain. Every other domain keeps using the Frontend policy.

Use this when one site needs sources the others shouldn't allow, such as a microsite with its own analytics or a campaign domain embedding a third-party widget.

## Create a domain policy

1. Open the **CSP Management** section.
2. On the **Frontend** node, open the actions menu (**...**) and choose **Add Domain Policy**.
3. Pick a domain. The list shows every hostname from Culture and Hostnames that doesn't have a policy yet, with its culture and the content node it's assigned to.
4. The editor opens with a copy of the Frontend policy. Change it, then click **Save**.

Nothing is created until you save. After saving, the policy appears under **Frontend** in the tree.

Domain policies use the same editor as the global policies: sources, settings (report-only, reporting, upgrade insecure requests) and the evaluator all work the same way. Use **Open content item** to jump to the content node the domain belongs to.

## Switch a domain back to the Frontend policy

The status toggle under **Settings** switches a domain between its own policy and the Frontend policy, so you can compare the two without deleting anything. Turned off, the domain uses the Frontend policy (or gets no header with [`NoHeader`](configuration#disableddomainpolicybehavior)) and shows as **(inactive)** in the tree. Turn it back on and save to apply the domain policy again.

{: .warning }
A CSP is not an isolation boundary between domains that share an origin. Path-based domains such as `example.com/en` and `example.com/fr` are one origin to the browser, so a page under one can load or script a page under the other whatever their policies say. Give each its own hostname when you need them kept apart.

{: .note }
Only hostname domains can have a policy. Wildcard (culture-only) domains are not listed, and each domain can have one policy.

## Which policy applies

| Request | Policy used |
|---|---|
| Backoffice (`/umbraco`) | Backoffice policy. Domain policies never apply. |
| Routed through a domain with an enabled domain policy | That domain policy |
| Routed through a domain with a disabled domain policy | Frontend policy, or no header (see [`DisabledDomainPolicyBehavior`](configuration#disableddomainpolicybehavior)) |
| Routed through a domain without a domain policy | Frontend policy |
| Not routed through a domain (static files, APIs, unmatched hosts) | Frontend policy |

The domain is the one Umbraco's routing matched for the request (`PublishedRequest.Domain`), so a domain policy applies to the pages Umbraco renders under that domain.

## Delete a domain policy

Open the policy and click **Delete domain policy**, then confirm. Requests on that domain go back to the Frontend policy straight away. The global Frontend and Backoffice policies cannot be deleted.

## Renamed and removed domains

A domain policy is tied to the domain *name*, because Umbraco doesn't keep a stable identifier for domains. If you rename a domain or remove it, its policy stops applying and shows as **Removed domain (...)** under Frontend.

The policy is kept rather than deleted, so a domain removed by mistake gets its policy back once it's re-added with the same name. Delete the policy when you no longer need it.

{: .warning }
Renaming a domain, for example from `example.com` to `www.example.com`, orphans its policy. Create a policy for the new name, or rename the domain back.

## Load-balanced sites and uSync

Saving or deleting a domain policy clears the cached policy on every server, like the global policies.

With [uSync](../integrations/usync), domain policies are exported and imported with the other CSP definitions, and deleting one deletes it on the next import. Because policies follow the domain name, a policy only applies on environments that use the same hostname. Import the domains before the policies, as an import fails for a domain that doesn't exist on the target.

## Extending

[`CspWritingNotification`](../advanced/notification-events#cspwritingnotification) receives the domain policy when one applies, so its `CspDefinition.Id` is not always the Frontend id. Check `CspDefinition.DomainKey` to tell them apart. [`CspDeletedNotification`](../advanced/notification-events#cspdeletednotification) is raised when a domain policy is deleted.
