import { expect } from "@playwright/test";
import { test } from "@umbraco/playwright-testhelpers";
import { CspApiHelpers, CspDefinitionBuilder, CspTestHelpers, EntityActions, TestDomains, WorkspaceTabs } from "./helpers";
import { CspConstants } from "../src/constants";

const baseUrl = process.env.URL ?? "https://localhost:44370";

// Every test starts with no domain policies and a disabled frontend policy.
// The API calls authenticate with the backoffice cookies, which are refreshed once the backoffice
// has loaded, so open the section first.
test.beforeEach(async ({ page, umbracoUi }) => {
	await new CspTestHelpers(umbracoUi).goToSection();

	const api = new CspApiHelpers(page.request);
	await api.deleteAllDomainPolicies();
	await api.resetDefinition("frontend");
});

test.afterEach(async ({ page }) => {
	const api = new CspApiHelpers(page.request);
	await api.deleteAllDomainPolicies();
	await api.resetDefinition("frontend");
});

async function domainKeyOf(api: CspApiHelpers, name: string): Promise<string> {
	const domain = (await api.getDomains()).find((d) => d.name === name);
	expect(domain, `test site domain ${name} (seeded by uSync)`).toBeDefined();
	return domain!.key;
}

test.describe("Domain policies: tree and add action", () => {
	test("Add Domain Policy is offered on Frontend only", async ({ umbracoUi }) => {
		const csp = new CspTestHelpers(umbracoUi);

		await csp.openEntityActionFromMenu(CspConstants.policyTypes.frontend.label, EntityActions.addDomainPolicy);
		await expect(csp.addDomainPolicyModal().locator("umb-body-layout")).toBeVisible();
		await csp.addDomainPolicyModal().getByRole("button", { name: "Cancel" }).click();
		await expect(csp.addDomainPolicyModal()).toHaveCount(0);

		const backOffice = csp.treeItem(CspConstants.policyTypes.backoffice.label);
		await backOffice.hover();
		await expect(backOffice.getByRole("button", { name: EntityActions.addDomainPolicy })).toHaveCount(0);
	});

	test("The modal lists only domains without a policy, with culture and content", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		await api.createDomainPolicy(CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enUs)).build());
		const csp = new CspTestHelpers(umbracoUi);
		await umbracoUi.reloadPage();

		await csp.openEntityActionFromMenu(CspConstants.policyTypes.frontend.label, EntityActions.addDomainPolicy);

		const modal = csp.addDomainPolicyModal();
		const enGb = modal.locator(`uui-ref-node[name="${TestDomains.enGb}"]`);
		await expect(enGb).toBeVisible();
		await expect(enGb).toHaveAttribute("detail", /en-GB · Homepage/);
		await expect(modal.locator(`uui-ref-node[name="${TestDomains.hostname}"]`)).toBeVisible();
		await expect(modal.locator(`uui-ref-node[name="${TestDomains.enUs}"]`)).toHaveCount(0);
	});
});

test.describe("Domain policies: create, edit, delete", () => {
	test("Creating from the tree opens a draft that is only saved on Save", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		const csp = new CspTestHelpers(umbracoUi);

		await csp.openEntityActionFromMenu(CspConstants.policyTypes.frontend.label, EntityActions.addDomainPolicy);
		await csp.addDomainPolicyModal().locator(`uui-ref-node[name="${TestDomains.hostname}"]`).click();

		// A draft: the create route, the draft notice, and nothing persisted yet.
		await expect(umbracoUi.page).toHaveURL(/\/workspace\/csp-policy\/create\//);
		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${TestDomains.hostname} CSP Management`);
		// No explanatory notice, just the link to the domain's content node.
		const domainInfo = umbracoUi.page.locator('[data-mark="csp-domain-info"]');
		await expect(domainInfo).toHaveAttribute("headline", "Linked content");
		await expect(domainInfo.getByRole("link", { name: "Open content item" })).toBeVisible();
		await expect(domainInfo.locator("p")).toHaveCount(0);
		expect(await api.getDomainPolicies()).toHaveLength(0);

		await csp.workspace().getByRole("button", { name: "Save", exact: true }).click();

		// Saved: the server assigned the id, the route switched to edit, the tree shows the child.
		await expect(umbracoUi.page).toHaveURL(/\/workspace\/csp-policy\/edit\/[0-9a-f-]{36}/);
		const policies = await api.getDomainPolicies();
		expect(policies).toHaveLength(1);
		expect(policies[0].domainName).toBe(TestDomains.hostname);
		expect(umbracoUi.page.url()).toContain(policies[0].id);
		await csp.expandTreeItem(CspConstants.policyTypes.frontend.label);
		// A copy of the (disabled) Frontend policy, so the tree marks it inactive.
		await expect(csp.treeItemByLabel(`${TestDomains.hostname} (inactive)`)).toBeVisible();
		await expect(csp.workspace().getByRole("button", { name: "Delete domain policy", exact: true })).toBeVisible();
	});

	test("Editing a domain policy changes the header on that domain only", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		await api.saveDefinition(
			CspDefinitionBuilder.frontend().enabled().withSource("global.example.com", ["default-src"]).build(),
		);
		const policy = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enGb))
				.enabled()
				.withSource("'self'", ["default-src"])
				.build(),
		);
		const csp = new CspTestHelpers(umbracoUi);
		await umbracoUi.reloadPage();

		// Open it from the tree (child of Frontend) and add a source through the UI.
		await csp.expandTreeItem(CspConstants.policyTypes.frontend.label);
		await csp.treeItemByLabel(TestDomains.enGb).click();
		await expect(umbracoUi.page).toHaveURL(new RegExp(policy.id));
		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${TestDomains.enGb} CSP Management`);

		await csp.workspace().getByRole("button", { name: "Add Source", exact: true }).click();
		const newSource = csp.workspace().getByLabel("CSP Source").last();
		await newSource.fill("domain-only.example.com");
		await csp.workspace().locator('uui-checkbox[label="script-src"]').last().click();
		await csp.workspace().getByRole("button", { name: "Save", exact: true }).click();
		await expect(umbracoUi.page.locator("uui-toast-notification").filter({ hasText: "Changes Saved" })).toBeVisible();

		const onDomain = await page.request.get(`${baseUrl}${TestDomains.enGb}/`, { ignoreHTTPSErrors: true });
		const elsewhere = await page.request.get(`${baseUrl}${TestDomains.enUs}/`, { ignoreHTTPSErrors: true });
		expect(onDomain.headers()["content-security-policy"]).toContain("domain-only.example.com");
		expect(onDomain.headers()["content-security-policy"]).not.toContain("global.example.com");
		expect(elsewhere.headers()["content-security-policy"]).toContain("global.example.com");
		expect(elsewhere.headers()["content-security-policy"]).not.toContain("domain-only.example.com");
	});

	test("Deleting a domain policy asks for confirmation and falls back to Frontend", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		await api.saveDefinition(
			CspDefinitionBuilder.frontend().enabled().withSource("global.example.com", ["default-src"]).build(),
		);
		const policy = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enGb))
				.enabled()
				.withSource("domain-only.example.com", ["default-src"])
				.build(),
		);
		const csp = new CspTestHelpers(umbracoUi);
		await umbracoUi.goToBackOffice();
		await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/edit/${policy.id}`);
		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${TestDomains.enGb} CSP Management`);

		// Cancelling the confirmation keeps the policy.
		await csp.workspace().getByRole("button", { name: "Delete domain policy", exact: true }).click();
		await umbracoUi.page.locator("umb-confirm-modal").getByRole("button", { name: "Cancel" }).click();
		expect(await api.getDomainPolicies()).toHaveLength(1);

		await csp.workspace().getByRole("button", { name: "Delete domain policy", exact: true }).click();
		await umbracoUi.page.locator("umb-confirm-modal").getByRole("button", { name: "Delete domain policy", exact: true }).click();

		await expect(umbracoUi.page).toHaveURL(new RegExp(CspConstants.policyTypes.frontend.value));
		expect(await api.getDomainPolicies()).toHaveLength(0);
		await expect(csp.treeItemByLabel(TestDomains.enGb)).toHaveCount(0);

		const onDomain = await page.request.get(`${baseUrl}${TestDomains.enGb}/`, { ignoreHTTPSErrors: true });
		expect(onDomain.headers()["content-security-policy"]).toContain("global.example.com");
	});

	// umbConfirmModal renders string content as HTML, so the domain name (user content) must reach it
	// as text. The API response is rewritten so the name carries markup.
	test("The delete confirmation shows the domain name as text, not HTML", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		const policy = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enGb)).build(),
		);
		const markup = '<img src="x" data-mark="csp-injected" onerror="window.__cspXss = true">';
		await umbracoUi.page.route(`**/umbraco/csp/api/v1/Definitions/${policy.id}`, async (route) => {
			const response = await route.fetch();
			const body = await response.json();
			await route.fulfill({ response, json: { ...body, domainName: markup } });
		});

		const csp = new CspTestHelpers(umbracoUi);
		await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/edit/${policy.id}`);
		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${markup} CSP Management`);

		await csp.workspace().getByRole("button", { name: "Delete domain policy", exact: true }).click();
		const confirm = umbracoUi.page.locator("umb-confirm-modal");
		await expect(confirm).toContainText(markup);
		await expect(confirm.locator('[data-mark="csp-injected"]')).toHaveCount(0);
		expect(await umbracoUi.page.evaluate(() => (window as unknown as { __cspXss?: boolean }).__cspXss)).toBeUndefined();
		await confirm.getByRole("button", { name: "Cancel" }).click();
	});

	// A draft waits for the Frontend policy and the domain list. A response that arrives after the
	// user has opened another policy must not turn that policy into a draft: the draft counts as
	// unsaved changes, so Save would be enabled and would create a domain policy instead.
	test("Opening another policy while a draft is loading keeps that policy", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		const domainKey = await domainKeyOf(api, TestDomains.hostname);

		let releaseDomains!: () => void;
		const domainsReleased = new Promise<void>((resolve) => (releaseDomains = resolve));
		let markDomainsRequested!: () => void;
		const domainsRequested = new Promise<void>((resolve) => (markDomainsRequested = resolve));
		await umbracoUi.page.route("**/umbraco/csp/api/v1/Domains", async (route) => {
			markDomainsRequested();
			await domainsReleased;
			await route.continue();
		});

		const csp = new CspTestHelpers(umbracoUi);
		await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/create/${domainKey}`);
		await domainsRequested;

		await csp.treeItemByLabel(CspConstants.policyTypes.frontend.label).click();
		await expect(umbracoUi.page).toHaveURL(new RegExp(`/edit/${CspConstants.policyTypes.frontend.value}$`));
		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${CspConstants.policyTypes.frontend.label} CSP Management`);

		// Let the draft's request finish now that the user has moved on.
		const domainsResponse = umbracoUi.page.waitForResponse("**/umbraco/csp/api/v1/Domains");
		releaseDomains();
		await domainsResponse;
		// The response has arrived; give the page a moment to apply it before checking nothing changed.
		await umbracoUi.page.waitForTimeout(500);

		await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${CspConstants.policyTypes.frontend.label} CSP Management`);
		await expect(umbracoUi.page.locator('[data-mark="csp-domain-info"]')).toHaveCount(0);
		await expect(csp.workspace().getByRole("button", { name: "Save", exact: true })).toBeDisabled();
		expect(await api.getDomainPolicies()).toHaveLength(0);
	});
});

test.describe("Domain policies: switching back to the Frontend policy", () => {
	test("Turning a domain policy off falls back to Frontend, turning it on overrides again", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		await api.saveDefinition(
			CspDefinitionBuilder.frontend().enabled().withSource("global.example.com", ["default-src"]).build(),
		);
		const policy = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enGb))
				.enabled()
				.withSource("domain-only.example.com", ["default-src"])
				.build(),
		);
		const csp = new CspTestHelpers(umbracoUi);
		const headerOnDomain = async () =>
			(await page.request.get(`${baseUrl}${TestDomains.enGb}/`, { ignoreHTTPSErrors: true })).headers()[
				"content-security-policy"
			];

		await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/edit/${policy.id}`);
		await csp.workspaceTab(WorkspaceTabs.settings).click();
		const summary = csp.workspace().locator('[data-mark="csp-status-summary"]');
		const toggle = csp.workspace().locator('[data-mark="csp-status-toggle"]');
		await expect(summary).toHaveText(`${TestDomains.enGb} uses this policy.`);
		await expect(toggle).toHaveAttribute("label", "Use Domain Policy");

		// Off: the domain uses the Frontend policy, and the tree marks the policy inactive.
		await toggle.click();
		await expect(summary).toHaveText(`${TestDomains.enGb} uses the Frontend policy.`);
		await expect(toggle).toHaveAttribute("label", "Use Frontend Policy");
		await csp.workspace().getByRole("button", { name: "Save", exact: true }).click();
		await expect(umbracoUi.page.locator("uui-toast-notification").filter({ hasText: "Changes Saved" })).toBeVisible();
		await csp.expandTreeItem(CspConstants.policyTypes.frontend.label);
		await expect(csp.treeItemByLabel(`${TestDomains.enGb} (inactive)`)).toBeVisible();
		expect(await headerOnDomain()).toContain("global.example.com");
		expect(await headerOnDomain()).not.toContain("domain-only.example.com");

		// On again: the domain policy overrides the Frontend policy.
		await toggle.click();
		await csp.workspace().getByRole("button", { name: "Save", exact: true }).click();
		await expect(csp.treeItemByLabel(TestDomains.enGb)).toBeVisible();
		expect(await headerOnDomain()).toContain("domain-only.example.com");
		expect(await headerOnDomain()).not.toContain("global.example.com");
	});
});

test.describe("Domain policies: moving an orphaned policy", () => {
	const renamedHostname = "csp-test-renamed.localhost:44370";

	test("A policy orphaned by a rename can be moved to the new hostname", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		const domain = (await api.getDomains()).find((d) => d.name === TestDomains.hostname);
		expect(domain?.rootContentKey, `test site domain ${TestDomains.hostname} (seeded by uSync)`).toBeTruthy();
		const documentKey = domain!.rootContentKey!;
		const orphan = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(domain!.key).enabled().withSource("moved.example.com", ["default-src"]).build(),
		);

		// Rename the hostname in Culture and Hostnames; the original is restored whatever happens.
		const original = await api.getDocumentDomains(documentKey);
		await api.setDocumentDomains(documentKey, {
			...original,
			domains: original.domains.map((d) =>
				d.domainName === TestDomains.hostname ? { ...d, domainName: renamedHostname } : d,
			),
		});

		try {
			const csp = new CspTestHelpers(umbracoUi);
			await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/edit/${orphan.id}`);
			const move = umbracoUi.page.locator('[data-mark="csp-move-domain-policy"]');
			const moveButton = move.getByRole("button", { name: "Move to domain...", exact: true });
			await expect(moveButton).toBeEnabled();

			// The move copies the stored policy, so it waits for unsaved changes to be saved or discarded.
			await csp.workspace().getByRole("button", { name: "Add Source", exact: true }).click();
			await expect(moveButton).toBeDisabled();
			await expect(move).toContainText("Save or discard your changes");
			await csp.workspace().getByRole("button", { name: "Delete", exact: true }).last().click();
			await umbracoUi.page.locator("umb-confirm-modal").getByRole("button", { name: "Delete", exact: true }).click();
			await expect(moveButton).toBeEnabled();

			await moveButton.click();
			const modal = csp.addDomainPolicyModal();
			await expect(modal.locator("umb-body-layout")).toHaveAttribute("headline", "Move policy to domain");
			await modal.locator(`uui-ref-node[name="${renamedHostname}"]`).click();

			await expect(umbracoUi.page.locator("uui-toast-notification").filter({ hasText: "Policy moved" })).toBeVisible();
			await expect(csp.workspaceEditor()).toHaveAttribute("headline", `${renamedHostname} CSP Management`);
			await expect(move).toHaveCount(0);

			const policies = await api.getDomainPolicies();
			expect(policies).toHaveLength(1);
			expect(policies[0].domainName).toBe(renamedHostname);
			expect(policies[0].isOrphaned).toBe(false);
			expect(policies[0].id).not.toBe(orphan.id);
			expect(umbracoUi.page.url()).toContain(policies[0].id);

			await csp.expandTreeItem(CspConstants.policyTypes.frontend.label);
			await expect(csp.treeItemByLabel(renamedHostname)).toBeVisible();
		} finally {
			await api.setDocumentDomains(documentKey, original);
		}
	});

	test("A policy whose domain still exists offers no move", async ({ umbracoUi, page }) => {
		const api = new CspApiHelpers(page.request);
		const policy = await api.createDomainPolicy(
			CspDefinitionBuilder.domainPolicy(await domainKeyOf(api, TestDomains.enGb)).build(),
		);

		await umbracoUi.page.goto(`${baseUrl}/umbraco/section/csp-manager/workspace/csp-policy/edit/${policy.id}`);
		await expect(new CspTestHelpers(umbracoUi).workspaceEditor()).toHaveAttribute("headline", `${TestDomains.enGb} CSP Management`);
		await expect(umbracoUi.page.locator('[data-mark="csp-domain-info"]')).toBeVisible();
		await expect(umbracoUi.page.locator('[data-mark="csp-move-domain-policy"]')).toHaveCount(0);
	});
});
