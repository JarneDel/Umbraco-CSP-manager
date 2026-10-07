using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Services;
using Umbraco.Community.CSPManager.Tests.Helpers;

namespace Umbraco.Community.CSPManager.Tests.Services;

// The header content rules, enforced by the service itself so they hold for every caller (uSync,
// custom code), not just the management API.
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class CspServiceWriteValidationTests : UmbracoIntegrationTestWithContent
{
	private ICspService _cspService;
	private IDomain _domain;

	protected override void CustomTestSetup(IUmbracoBuilder builder)
	{
		builder.AddComposers();
	}

	protected override void SetUpTestConfiguration(IConfigurationBuilder configBuilder)
	{
		base.SetUpTestConfiguration(configBuilder);
		configBuilder.AddInMemoryCollection(new Dictionary<string, string>
		{
			["Umbraco:CMS:Unattended:PackageMigrationsUnattended"] = "false"
		});
	}

	[SetUp]
	public async Task SetUpDomain()
	{
		await CspTestMigrationHelper.RunMigrationsAsync(GetRequiredService<IMigrationPlanExecutor>(), ScopeProvider, GetRequiredService<IKeyValueService>());
		_cspService = GetRequiredService<ICspService>();
		_domain = (await CspTestDomainHelper.AssignDomainsAsync(GetRequiredService<IDomainService>(), Textpage.Key, ["a.example.com"])).Single();
	}

	// ── Header content ───────────────────────────────────────────────────────

	[TestCase("x; script-src *", TestName = "A source that splices in a directive is rejected")]
	[TestCase("https://example.com\r\nX-Injected: 1", TestName = "A source with CR/LF is rejected")]
	[TestCase("a.example.com,b.example.com", TestName = "A source with a comma is rejected")]
	public async Task SaveCspDefinitionAsync_GlobalPolicyWithAnInvalidSource_IsRejectedAndNotStored(string source)
	{
		var ex = Assert.ThrowsAsync<CspDefinitionValidationException>(() => _cspService.SaveCspDefinitionAsync(
			Frontend(new CspDefinitionSource { Source = source, Directives = [Constants.Directives.DefaultSource] }),
			CancellationToken.None));

		var stored = await _cspService.GetCspDefinitionAsync(isBackOfficeRequest: false, CancellationToken.None);
		Assert.Multiple(() =>
		{
			Assert.That(ex!.MemberName, Is.EqualTo(nameof(CspDefinition.Sources)));
			Assert.That(ex.Message, Does.Contain("must be a single token").And.Not.Contain("\n"));
			Assert.That(stored.Sources.Select(s => s.Source), Does.Not.Contain(source));
		});
	}

	[Test]
	public async Task SaveCspDefinitionAsync_DomainPolicyWithAnInvalidSource_IsRejectedAndNotCreated()
	{
		var policy = new CspDefinition
		{
			DomainKey = _domain.PolicyKey(),
			Sources = [new CspDefinitionSource { Source = "'self';script-src *", Directives = [Constants.Directives.DefaultSource] }]
		};

		Assert.ThrowsAsync<CspDefinitionValidationException>(() => _cspService.SaveCspDefinitionAsync(policy, CancellationToken.None));
		Assert.That(await _cspService.GetAllDomainPoliciesAsync(CancellationToken.None), Is.Empty);
	}

	[TestCase("not-a-directive", TestName = "An unknown directive is rejected")]
	[TestCase("default-src\r\nX-Injected", TestName = "A directive with CR/LF is rejected")]
	public void SaveCspDefinitionAsync_WithAnInvalidDirective_IsRejected(string directive)
	{
		var ex = Assert.ThrowsAsync<CspDefinitionValidationException>(() => _cspService.SaveCspDefinitionAsync(
			Frontend(new CspDefinitionSource { Source = "'self'", Directives = [directive] }),
			CancellationToken.None));
		Assert.That(ex!.Message, Does.Contain("Unknown directive"));
	}

	[TestCase("report-uri", "https://example.com/r; script-src *", TestName = "A report-uri value with ';' is rejected")]
	[TestCase("report-uri", "ftp://example.com/r", TestName = "A report-uri value with a non-http scheme is rejected")]
	[TestCase("report-to", "https://example.com/r", TestName = "A report-to value that isn't an endpoint name is rejected")]
	[TestCase("report-to", "csp\r\nendpoint", TestName = "A report-to value with CR/LF is rejected")]
	[TestCase("none", "https://example.com/r", TestName = "An unknown reporting directive is rejected")]
	public void SaveCspDefinitionAsync_WithInvalidReporting_IsRejected(string directive, string reportUri)
	{
		var definition = Frontend();
		definition.ReportingDirective = directive;
		definition.ReportUri = reportUri;

		Assert.ThrowsAsync<CspDefinitionValidationException>(() => _cspService.SaveCspDefinitionAsync(definition, CancellationToken.None));
	}

	[Test]
	public async Task SaveCspDefinitionAsync_WithoutAReportingDirective_IgnoresAStaleReportUri()
	{
		var definition = Frontend();
		definition.ReportUri = "left over; from before";

		await _cspService.SaveCspDefinitionAsync(definition, CancellationToken.None);

		Assert.That((await _cspService.GetCspDefinitionAsync(isBackOfficeRequest: false, CancellationToken.None)).Enabled, Is.True);
	}

	[Test]
	public async Task SaveCspDefinitionAsync_WhitespaceOnlySources_AreDroppedNotRejected()
	{
		await _cspService.SaveCspDefinitionAsync(
			Frontend(
				new CspDefinitionSource { Source = "'self'", Directives = [Constants.Directives.DefaultSource] },
				new CspDefinitionSource { Source = " ", Directives = [Constants.Directives.DefaultSource] },
				new CspDefinitionSource { Source = "", Directives = [Constants.Directives.DefaultSource] }),
			CancellationToken.None);

		var stored = await _cspService.GetCspDefinitionAsync(isBackOfficeRequest: false, CancellationToken.None);
		Assert.That(stored.Sources.Select(s => s.Source), Is.EquivalentTo(new[] { "'self'" }));
	}

	private static CspDefinition Frontend(params CspDefinitionSource[] sources) => new()
	{
		Id = Constants.DefaultFrontEndId,
		Enabled = true,
		Sources = [.. sources]
	};
}
