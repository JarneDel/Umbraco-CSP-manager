using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Builders;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Models.Api;
using Umbraco.Community.CSPManager.Services;
using Umbraco.Community.CSPManager.Tests.Helpers;
using DefinitionsControllerType = Umbraco.Community.CSPManager.Controllers.DefinitionsController;
using DomainsControllerType = Umbraco.Community.CSPManager.Controllers.CspDomainsController;
using UmbConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Community.CSPManager.Tests.Controllers.DefinitionsController;

/// <summary>
/// The domain policy endpoints: authorization on every one of them, the create/save/delete
/// rules (including the id-hijack cases), and the 400/404 paths.
/// </summary>
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
internal class DomainPolicyEndpointTests : CspManagementApiTest<DefinitionsControllerType>
{
	protected override Expression<Func<DefinitionsControllerType, object>> MethodSelector { get; set; } = x => x.GetDomainPolicies(CancellationToken.None);

	private static int _userCounter;

	private ICspService _cspService;
	private IContent _siteA;
	private IContent _siteB;
	private IDomain _domainA;
	private IDomain _domainB;

	[SetUp]
	public async Task SetUpDomains()
	{
		_cspService = GetRequiredService<ICspService>();

		var contentType = ContentTypeBuilder.CreateSimpleContentType("site", "Site");
		contentType.AllowedAsRoot = true;
		var contentTypeResult = await GetRequiredService<IContentTypeService>().CreateAsync(contentType, UmbConstants.Security.SuperUserKey);
		Assert.That(contentTypeResult.Success, Is.True);

		var contentService = GetRequiredService<IContentService>();
		_siteA = ContentBuilder.CreateSimpleContent(contentType, "Site A");
		contentService.Save(_siteA);
		_siteB = ContentBuilder.CreateSimpleContent(contentType, "Site B");
		contentService.Save(_siteB);

		var domainService = GetRequiredService<IDomainService>();
		_domainA = (await CspTestDomainHelper.AssignDomainsAsync(domainService, _siteA.Key, ["a.example.com"])).Single();
		_domainB = (await CspTestDomainHelper.AssignDomainsAsync(domainService, _siteB.Key, ["b.example.com"])).Single();
	}

	private async Task AuthenticateAsAdminAsync()
		=> await AuthenticateClientAsync(Client, $"domain-admin{Interlocked.Increment(ref _userCounter)}@example.com", UserPassword, true);

	private async Task AuthenticateAsEditorAsync()
		=> await AuthenticateClientAsync(Client, $"domain-editor{Interlocked.Increment(ref _userCounter)}@example.com", UserPassword, UmbConstants.Security.EditorGroupKey);

	private string DefinitionsUrl(Expression<Func<DefinitionsControllerType, object>> selector) => GetManagementApiUrl(selector);

	private async Task<CspApiDefinition> ReadDefinitionAsync(HttpResponseMessage response)
		=> await response.Content.ReadFromJsonAsync<CspApiDefinition>(JsonSerializerOptions);

	private static CspApiDefinition NewPolicyBody(Guid domainKey, Guid? id = null) => new()
	{
		Id = id ?? Guid.Empty,
		DomainKey = domainKey,
		Enabled = true,
		Sources = [new CspApiDefinitionSource { Source = "'self'", Directives = [Constants.Directives.DefaultSource] }]
	};

	// ── Authorization ────────────────────────────────────────────────────────

	[Test]
	public async Task EveryDomainPolicyEndpoint_WithoutSectionAccess_ReturnsForbidden()
	{
		var policy = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsEditorAsync();

		var requests = new (string Name, Func<Task<HttpResponseMessage>> Send)[]
		{
			("GET Definitions?domainKey", () => Client.GetAsync(DefinitionsUrl(x => x.GetDefinition(false, _domainA.PolicyKey(), CancellationToken.None)))),
			("GET Definitions/{id}", () => Client.GetAsync(DefinitionsUrl(x => x.GetDefinitionById(policy.Id, CancellationToken.None)))),
			("GET Definitions/domain-policies", () => Client.GetAsync(DefinitionsUrl(x => x.GetDomainPolicies(CancellationToken.None)))),
			("POST Definitions/create-from-frontend", () => Client.PostAsync(DefinitionsUrl(x => x.CreateFromFrontend(_domainB.PolicyKey(), CancellationToken.None)), null)),
			("POST Definitions/save (create)", () => Client.PostAsync(DefinitionsUrl(x => x.SaveDefinition(null!, CancellationToken.None)), JsonContent.Create(NewPolicyBody(_domainB.PolicyKey())))),
			("DELETE Definitions/{id}", () => Client.DeleteAsync(DefinitionsUrl(x => x.DeleteDefinition(policy.Id, CancellationToken.None)))),
			("POST Definitions/{id}/move", () => Client.PostAsync(DefinitionsUrl(x => x.MoveDomainPolicy(policy.Id, _domainB.PolicyKey(), CancellationToken.None)), null)),
			("GET Domains", () => Client.GetAsync(GetManagementApiUrl<DomainsControllerType>(x => x.GetDomains(CancellationToken.None)))),
		};

		foreach (var (name, send) in requests)
		{
			var response = await send();
			Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), name);
		}

		Assert.That(await _cspService.GetCspDefinitionAsync(policy.Id, CancellationToken.None), Is.Not.Null, "the forbidden delete must not have run");
		Assert.That(await _cspService.GetCspDefinitionForDomainAsync(_domainB.PolicyKey(), CancellationToken.None), Is.Null, "the forbidden creates must not have run");
	}

	// ── Create ───────────────────────────────────────────────────────────────

	[Test]
	public async Task Save_WithEmptyIdAndDomainKey_CreatesWithAServerAssignedId()
	{
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.SaveDefinition(null!, CancellationToken.None)), JsonContent.Create(NewPolicyBody(_domainA.PolicyKey())));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var created = await ReadDefinitionAsync(response);
		Assert.Multiple(() =>
		{
			Assert.That(created.Id, Is.Not.EqualTo(Guid.Empty));
			Assert.That(created.DomainKey, Is.EqualTo(_domainA.PolicyKey()));
			Assert.That(created.DomainName, Is.EqualTo("a.example.com"));
			Assert.That(created.RootContentKey, Is.EqualTo(_siteA.Key));
			Assert.That(created.DisabledDomainPolicyBehavior, Is.EqualTo(DisabledDomainPolicyBehavior.FallbackToGlobal));
		});
		var stored = await _cspService.GetCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		Assert.That(stored?.Id, Is.EqualTo(created.Id));
	}

	[Test]
	public async Task Save_WithAClientChosenIdThatDoesNotExist_ReturnsNotFound()
	{
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.SaveDefinition(null!, CancellationToken.None)), JsonContent.Create(NewPolicyBody(_domainA.PolicyKey(), Guid.NewGuid())));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
		Assert.That(await _cspService.GetCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None), Is.Null);
	}

	[Test]
	public async Task Save_SecondPolicyForTheSameDomain_ReturnsBadRequest()
	{
		await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.SaveDefinition(null!, CancellationToken.None)), JsonContent.Create(NewPolicyBody(_domainA.PolicyKey())));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("A policy already exists for this domain."));
		Assert.That(await _cspService.GetAllDomainPoliciesAsync(CancellationToken.None), Has.Count.EqualTo(1));
	}

	[Test]
	public async Task CreateFromFrontend_CopiesTheFrontendPolicy()
	{
		await _cspService.SaveCspDefinitionAsync(new CspDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Enabled = true,
			Sources = [new CspDefinitionSource { Source = "frontend.example.com", Directives = [Constants.Directives.ScriptSource] }]
		}, CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.CreateFromFrontend(_domainA.PolicyKey(), CancellationToken.None)), null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var created = await ReadDefinitionAsync(response);
		Assert.Multiple(() =>
		{
			Assert.That(created.Id, Is.Not.EqualTo(Constants.DefaultFrontEndId));
			Assert.That(created.DomainKey, Is.EqualTo(_domainA.PolicyKey()));
			Assert.That(created.Sources.Select(s => s.Source), Is.EqualTo(new[] { "frontend.example.com" }));
		});
	}

	// ── Update ───────────────────────────────────────────────────────────────

	[Test]
	public async Task Save_ExistingPolicyWithItsOwnIdAndDomain_ReturnsOk()
	{
		var policy = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var body = NewPolicyBody(_domainA.PolicyKey(), policy.Id);
		body.Enabled = false;
		var response = await Client.PostAsync(DefinitionsUrl(x => x.SaveDefinition(null!, CancellationToken.None)), JsonContent.Create(body));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var stored = await _cspService.GetCspDefinitionAsync(policy.Id, CancellationToken.None);
		Assert.That(stored.Enabled, Is.False);
	}

	// ── Read ─────────────────────────────────────────────────────────────────

	[Test]
	public async Task GetDefinitionById_And_GetDefinitionForDomain_ReturnTheDomainPolicy()
	{
		var policy = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var byId = await Client.GetAsync(DefinitionsUrl(x => x.GetDefinitionById(policy.Id, CancellationToken.None)));
		var byDomain = await Client.GetAsync(DefinitionsUrl(x => x.GetDefinition(false, _domainA.PolicyKey(), CancellationToken.None)));

		Assert.Multiple(async () =>
		{
			Assert.That(byId.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(byDomain.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That((await ReadDefinitionAsync(byId)).DomainName, Is.EqualTo("a.example.com"));
			Assert.That((await ReadDefinitionAsync(byDomain)).Id, Is.EqualTo(policy.Id));
		});
	}

	[Test]
	public async Task GetDefinitionById_And_GetDefinitionForDomain_WhenMissing_ReturnNotFound()
	{
		await AuthenticateAsAdminAsync();

		var byId = await Client.GetAsync(DefinitionsUrl(x => x.GetDefinitionById(Guid.NewGuid(), CancellationToken.None)));
		var byDomain = await Client.GetAsync(DefinitionsUrl(x => x.GetDefinition(false, _domainB.PolicyKey(), CancellationToken.None)));

		Assert.Multiple(() =>
		{
			Assert.That(byId.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
			Assert.That(byDomain.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
		});
	}

	[Test]
	public async Task GetDomainPolicies_ListsPoliciesAndFlagsOrphans()
	{
		var policyA = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		var policyB = await _cspService.CreateCspDefinitionForDomainAsync(_domainB.PolicyKey(), CancellationToken.None);
		// Remove domain B from its node: its policy is kept but orphaned.
		await CspTestDomainHelper.AssignDomainsAsync(GetRequiredService<IDomainService>(), _siteB.Key, []);
		await AuthenticateAsAdminAsync();

		var response = await Client.GetAsync(Url);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var policies = await response.Content.ReadFromJsonAsync<List<CspApiDomainPolicy>>(JsonSerializerOptions);
		Assert.That(policies, Has.Count.EqualTo(2));
		var a = policies.Single(p => p.Id == policyA.Id);
		var b = policies.Single(p => p.Id == policyB.Id);
		Assert.Multiple(() =>
		{
			Assert.That(a.DomainName, Is.EqualTo("a.example.com"));
			Assert.That(a.IsOrphaned, Is.False);
			Assert.That(b.DomainName, Is.Null);
			Assert.That(b.IsOrphaned, Is.True);
			Assert.That(b.DomainKey, Is.EqualTo(_domainB.PolicyKey()));
		});
	}

	[Test]
	public async Task GetDomains_ListsDomainsWithTheirContentCultureAndPolicy()
	{
		var policyA = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var response = await Client.GetAsync(GetManagementApiUrl<DomainsControllerType>(x => x.GetDomains(CancellationToken.None)));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var domains = await response.Content.ReadFromJsonAsync<List<CspDomainInfo>>(JsonSerializerOptions);
		var a = domains.Single(d => d.Key == _domainA.PolicyKey());
		var b = domains.Single(d => d.Key == _domainB.PolicyKey());
		Assert.Multiple(() =>
		{
			Assert.That(a.Name, Is.EqualTo("a.example.com"));
			Assert.That(a.Culture, Is.EqualTo("en-US"));
			Assert.That(a.RootContentKey, Is.EqualTo(_siteA.Key));
			Assert.That(a.RootContentName, Is.EqualTo("Site A"));
			Assert.That(a.HasCspPolicy, Is.True);
			Assert.That(a.CspDefinitionId, Is.EqualTo(policyA.Id));
			Assert.That(b.HasCspPolicy, Is.False);
			Assert.That(b.CspDefinitionId, Is.Null);
		});
	}

	// ── Delete ───────────────────────────────────────────────────────────────

	[Test]
	public async Task Delete_DomainPolicy_ReturnsOkThenNotFound()
	{
		var policy = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var first = await Client.DeleteAsync(DefinitionsUrl(x => x.DeleteDefinition(policy.Id, CancellationToken.None)));
		var second = await Client.DeleteAsync(DefinitionsUrl(x => x.DeleteDefinition(policy.Id, CancellationToken.None)));

		Assert.Multiple(async () =>
		{
			Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK));
			Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
			Assert.That(await _cspService.GetCspDefinitionAsync(policy.Id, CancellationToken.None), Is.Null);
		});
	}

	// The controller skips its 404 check for the global ids, so the service's refusal must surface as 400.
	[Test]
	public async Task Delete_GlobalPolicy_ReturnsBadRequest()
	{
		var id = Constants.DefaultFrontEndId;
		await _cspService.SaveCspDefinitionAsync(new CspDefinition { Id = Constants.DefaultFrontEndId }, CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var response = await Client.DeleteAsync(DefinitionsUrl(x => x.DeleteDefinition(id, CancellationToken.None)));

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(await _cspService.GetCspDefinitionAsync(id, CancellationToken.None), Is.Not.Null);
	}

	// ── Move ─────────────────────────────────────────────────────────────────

	[Test]
	public async Task Move_OrphanedPolicy_ReturnsThePolicyUnderItsNewDomain()
	{
		var orphan = await _cspService.CreateCspDefinitionForDomainAsync(_domainB.PolicyKey(), CancellationToken.None);
		// Rename b.example.com to c.example.com: the policy is orphaned, c has none.
		var domainC = (await CspTestDomainHelper.AssignDomainsAsync(GetRequiredService<IDomainService>(), _siteB.Key, ["c.example.com"])).Single();
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.MoveDomainPolicy(orphan.Id, domainC.PolicyKey(), CancellationToken.None)), null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
		var moved = await ReadDefinitionAsync(response);
		Assert.Multiple(async () =>
		{
			Assert.That(moved.Id, Is.Not.EqualTo(orphan.Id));
			Assert.That(moved.DomainKey, Is.EqualTo(domainC.PolicyKey()));
			Assert.That(moved.DomainName, Is.EqualTo("c.example.com"));
			Assert.That(moved.RootContentKey, Is.EqualTo(_siteB.Key));
			Assert.That(await _cspService.GetCspDefinitionAsync(orphan.Id, CancellationToken.None), Is.Null);
		});
	}

	[Test]
	public async Task Move_UnknownId_ReturnsNotFound()
	{
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.MoveDomainPolicy(Guid.NewGuid(), _domainA.PolicyKey(), CancellationToken.None)), null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
	}

	[Test]
	public async Task Move_PolicyWhoseDomainStillExists_ReturnsBadRequest()
	{
		var policy = await _cspService.CreateCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None);
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.MoveDomainPolicy(policy.Id, _domainB.PolicyKey(), CancellationToken.None)), null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That((await _cspService.GetCspDefinitionAsync(policy.Id, CancellationToken.None))?.DomainKey, Is.EqualTo(_domainA.PolicyKey()));
	}

	// The controller skips its 404 check for the global ids, so the service's refusal must surface as 400.
	[Test]
	public async Task Move_GlobalPolicy_ReturnsBadRequest()
	{
		await AuthenticateAsAdminAsync();

		var response = await Client.PostAsync(DefinitionsUrl(x => x.MoveDomainPolicy(Constants.DefaultFrontEndId, _domainA.PolicyKey(), CancellationToken.None)), null);

		Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
		Assert.That(await _cspService.GetCspDefinitionForDomainAsync(_domainA.PolicyKey(), CancellationToken.None), Is.Null);
	}
}
