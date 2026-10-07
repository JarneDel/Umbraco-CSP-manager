using System.Xml.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.uSync.Serializers;
using uSync.Core;
using uSync.Core.Models;
using uSync.Core.Serialization;

namespace Umbraco.Community.CSPManager.uSync.Tests;

[TestFixture]
public class CspDefinitionSerializerTests
{
	private static readonly Guid DomainKey = CspDomainKey.FromDomainName("a.example.com");

	private FakeCspService _cspService = null!;
	private CspDefinitionSerializer _serializer = null!;

	[SetUp]
	public void SetUp()
	{
		_cspService = new FakeCspService();
		_serializer = new CspDefinitionSerializer(NullLogger<CspDefinitionSerializer>.Instance, _cspService);
	}

	private static CspDefinition DomainPolicy(Guid? id = null)
	{
		var definitionId = id ?? Guid.NewGuid();
		return new CspDefinition
		{
			Id = definitionId,
			DomainKey = DomainKey,
			Enabled = true,
			ReportOnly = true,
			Sources = [new CspDefinitionSource { DefinitionId = definitionId, Source = "'self'", Directives = [Constants.Directives.DefaultSource, Constants.Directives.ScriptSource] }]
		};
	}

	private async Task<XElement> SerializeAsync(CspDefinition definition)
	{
		var attempt = await _serializer.SerializeAsync(definition, new SyncSerializerOptions());
		Assert.That(attempt.Success, Is.True, attempt.Message);
		return attempt.Item!;
	}

	[Test]
	public async Task Serialize_DomainPolicy_WritesItsDomainKeyAndADomainAlias()
	{
		var node = await SerializeAsync(DomainPolicy());

		Assert.Multiple(() =>
		{
			Assert.That(node.Element("Info")?.Element("DomainKey")?.Value, Is.EqualTo(DomainKey.ToString()));
			Assert.That(node.Attribute("Alias")?.Value, Is.EqualTo($"domain-{DomainKey:D}"));
		});
	}

	[Test]
	public async Task Serialize_GlobalPolicy_HasNoDomainKey()
	{
		var node = await SerializeAsync(new CspDefinition { Id = Constants.DefaultFrontEndId });

		Assert.Multiple(() =>
		{
			Assert.That(node.Element("Info")?.Element("DomainKey"), Is.Null, "global files must stay as they were");
			Assert.That(node.Attribute("Alias")?.Value, Is.EqualTo("front-end"));
		});
	}

	[Test]
	public async Task RoundTrip_DomainPolicyThatDoesNotExistYet_IsCreatedWithItsIdAndDomain()
	{
		var original = DomainPolicy();
		var node = await SerializeAsync(original);

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(attempt.Success, Is.True, attempt.Message);
		var imported = _cspService.Definitions[original.Id];
		Assert.Multiple(() =>
		{
			Assert.That(imported.DomainKey, Is.EqualTo(DomainKey));
			Assert.That(imported.IsBackOffice, Is.False);
			Assert.That(imported.Enabled, Is.True);
			Assert.That(imported.ReportOnly, Is.True);
			Assert.That(imported.Sources.Single().Source, Is.EqualTo("'self'"));
			Assert.That(imported.Sources.Single().Directives, Is.EqualTo(original.Sources.Single().Directives));
		});
	}

	[Test]
	public async Task Deserialize_DomainPolicyCreatedSeparatelyOnTheTarget_UpdatesThatPolicy()
	{
		var existing = DomainPolicy();
		existing.Enabled = false;
		_cspService.Definitions[existing.Id] = existing;
		var node = await SerializeAsync(DomainPolicy());

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(attempt.Success, Is.True, attempt.Message);
		Assert.That(_cspService.Definitions, Has.Count.EqualTo(1), "one policy per domain");
		Assert.That(_cspService.Definitions[existing.Id].Enabled, Is.True);
	}

	[Test]
	public async Task Deserialize_FileThatMovesAnExistingPolicyToAnotherDomain_Fails()
	{
		var existing = DomainPolicy();
		existing.DomainKey = CspDomainKey.FromDomainName("b.example.com");
		_cspService.Definitions[existing.Id] = existing;
		var node = await SerializeAsync(DomainPolicy(existing.Id));

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(attempt.Success, Is.False);
		Assert.That(_cspService.Definitions[existing.Id].DomainKey, Is.EqualTo(CspDomainKey.FromDomainName("b.example.com")));
	}

	[Test]
	public async Task Deserialize_DomainPolicyFlaggedAsBackOffice_IsImportedAsFrontend()
	{
		var node = await SerializeAsync(DomainPolicy());
		node.Element("Info")!.Element("IsBackOffice")!.Value = "true";

		await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(_cspService.Definitions.Values.Single().IsBackOffice, Is.False);
	}

	// An invalid file fails just that item, with the reason, instead of importing a value that
	// would splice a directive into the header or make the server drop the header altogether.
	[TestCase("'self'; script-src *", TestName = "A source that splices in a directive fails the item")]
	[TestCase("https://example.com\r\nX-Injected: 1", TestName = "A source with a line break fails the item")]
	public async Task Deserialize_FileWithAnInvalidSource_FailsWithTheReason(string source)
	{
		var policy = DomainPolicy();
		policy.Sources.Add(new CspDefinitionSource { DefinitionId = policy.Id, Source = source, Directives = [Constants.Directives.ScriptSource] });
		var node = await SerializeAsync(policy);

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.Multiple(() =>
		{
			Assert.That(attempt.Success, Is.False);
			Assert.That(attempt.Message, Does.StartWith("Invalid CSP definition:").And.Contain("must be a single token"));
			Assert.That(_cspService.Definitions, Is.Empty, "nothing is saved");
		});
	}

	[TestCase("report-uri", "/csp-report; script-src *")]
	[TestCase("report-to", "https://example.com/not-an-endpoint-name")]
	[TestCase("bogus", "/csp-report")]
	public async Task Deserialize_FileWithInvalidReporting_Fails(string directive, string reportUri)
	{
		var policy = DomainPolicy();
		policy.ReportingDirective = directive;
		policy.ReportUri = reportUri;
		var node = await SerializeAsync(policy);

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.Multiple(() =>
		{
			Assert.That(attempt.Success, Is.False);
			Assert.That(_cspService.Definitions, Is.Empty);
		});
	}

	[Test]
	public async Task Deserialize_FileWithAnUnknownDirective_Fails()
	{
		var node = await SerializeAsync(new CspDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources = [new CspDefinitionSource { DefinitionId = Constants.DefaultFrontEndId, Source = "'self'", Directives = ["not-a-directive"] }]
		});

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.Multiple(() =>
		{
			Assert.That(attempt.Success, Is.False);
			Assert.That(attempt.Message, Does.Contain("Unknown directive"));
		});
	}

	[Test]
	public async Task Deserialize_FileWithABlankSource_StillImports()
	{
		var policy = DomainPolicy();
		policy.Sources.Add(new CspDefinitionSource { DefinitionId = policy.Id, Source = " ", Directives = [Constants.Directives.ScriptSource] });
		var node = await SerializeAsync(policy);

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(attempt.Success, Is.True, attempt.Message);
	}

	[Test]
	public async Task Deserialize_UnknownIdWithoutDomainKey_Fails()
	{
		var node = await SerializeAsync(new CspDefinition { Id = Guid.NewGuid() });

		var attempt = await _serializer.DeserializeAsync(node, new SyncSerializerOptions());

		Assert.That(attempt.Success, Is.False);
		Assert.That(_cspService.Definitions, Is.Empty);
	}

	[Test]
	public async Task FindItemAsync_ByDomainAlias_FindsThePolicyForThatDomain()
	{
		var policy = DomainPolicy();
		_cspService.Definitions[policy.Id] = policy;

		var found = await _serializer.FindItemAsync($"domain-{DomainKey:D}");

		Assert.That(found?.Id, Is.EqualTo(policy.Id));
	}

	[Test]
	public async Task DeleteItemAsync_DomainPolicy_IsDeleted()
	{
		var policy = DomainPolicy();
		_cspService.Definitions[policy.Id] = policy;

		await _serializer.DeleteItemAsync(policy);

		Assert.That(_cspService.Deleted, Is.EqualTo(new[] { policy.Id }));
	}

	[Test]
	public async Task DeleteItemAsync_GlobalPolicy_IsLeftAlone()
	{
		await _serializer.DeleteItemAsync(new CspDefinition { Id = Constants.DefaultFrontEndId });

		Assert.That(_cspService.Deleted, Is.Empty);
	}

	// A delete marker written for a domain policy, imported on another environment, deletes it there.
	[Test]
	public async Task DeleteMarker_ForADomainPolicy_DeletesItOnImport()
	{
		var policy = DomainPolicy();
		_cspService.Definitions[policy.Id] = policy;
		var marker = (await _serializer.SerializeEmptyAsync(policy, SyncActionType.Delete, string.Empty)).Item!;

		var attempt = await _serializer.DeserializeAsync(marker, new SyncSerializerOptions());

		Assert.Multiple(() =>
		{
			Assert.That(attempt.Change, Is.EqualTo(ChangeType.Delete));
			Assert.That(_cspService.Deleted, Is.EqualTo(new[] { policy.Id }));
		});
	}
}
