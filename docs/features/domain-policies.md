---
title: Domain Policies
parent: Features
nav_order: 3
---

# Domain policies

Domain policies assign a dedicated Content Security Policy (CSP) to specific hostnames configured under **Culture and Hostnames**. When enabled, a domain policy overrides the default **Frontend** policy for requests routed through that domain; all other domains continue using the Frontend policy.

Use domain policies when specific sites require unique directives—such as dedicated analytics on a microsite or third-party embeds on a campaign page.

## Create a domain policy

1. Open the **CSP Management** section in the backoffice.
2. In the tree, open the context menu (`...`) on the **Frontend** node and select **Add Domain Policy**.
3. Select a domain from the dropdown. The list shows unassigned hostnames from Culture and Hostnames alongside their assigned content node and culture.
4. Adjust directives in the editor (pre-filled with a copy of the Frontend policy), then click **Save**.

The policy is created once saved and appears as a child of the Frontend node in the tree.

Domain policies support the same settings (Report-Only, Reporting, Upgrade Insecure Requests, Sources, and Evaluator) as global policies. Click **Open content item** in the editor header to navigate directly to the assigned Umbraco content node.

## Disable or re-enable a policy

Toggle the status switch under **Settings** to disable a domain policy without deleting it:

- **Disabled:** Displays as `(inactive)` in the tree. Requests fall back to the Frontend policy or emit no header, depending on [`DisabledDomainPolicyBehavior`](configuration#disableddomainpolicybehavior).
- **Enabled:** The domain policy applies to requests for this hostname.

{: .warning }
A CSP does not provide an origin isolation boundary. Path-based cultures on the same origin (such as `example.com/en` and `example.com/fr`) share execution context in the browser. To isolate policies, use separate hostnames.

{: .note }
Policies require a full hostname. Culture-only wildcard entries (`/`) cannot receive a domain policy, and each hostname supports only one policy.

## Policy resolution order

Umbraco matches policies against the resolved request domain (`PublishedRequest.Domain`):

| Request context | Applied policy |
|---|---|
| Backoffice (`/umbraco`) | Backoffice policy (domain policies never apply) |
| Routed domain with active domain policy | Domain policy |
| Routed domain with disabled domain policy | Frontend policy, or no header ([`DisabledDomainPolicyBehavior`](configuration#disableddomainpolicybehavior)) |
| Routed domain without domain policy | Frontend policy |
| Unrouted requests (static assets, custom APIs, unmatched hosts) | Frontend policy |

## Delete a policy

Open the policy, click **Delete domain policy**, and confirm. Traffic on that hostname reverts to the Frontend policy immediately. Global Frontend and Backoffice policies cannot be deleted.

## Renamed and removed domains

Policies map directly to the **hostname string**. If you rename or delete a hostname in Culture and Hostnames:

- The policy detaches and appears in the tree as **Removed domain (...)**.
- The policy is preserved rather than deleted. Re-adding the original hostname re-associates the policy automatically.
- To re-assign the policy after renaming a hostname, open the policy and click **Move to domain...**, then select the new hostname. This creates the policy under the new domain and cleans up the orphan. Ensure any unsaved edits are saved before moving.
- If a domain is permanently decommissioned, click **Delete domain policy** to remove the orphan.

## Load balancing and uSync

- **Cache invalidation:** Saving or deleting a domain policy clears the CSP cache across all load-balanced instances automatically.
- **[uSync](../integrations/usync):** Domain policies export and import alongside global definitions. Ensure target environments configure the required hostnames before importing policies.

## Extending

- [`CspWritingNotification`](../advanced/notification-events#cspwritingnotification) supplies the resolved policy instance. Inspect `CspDefinition.DomainKey` to distinguish domain policies from the global Frontend policy.
- [`CspDeletedNotification`](../advanced/notification-events#cspdeletednotification) fires whenever a domain policy is deleted.