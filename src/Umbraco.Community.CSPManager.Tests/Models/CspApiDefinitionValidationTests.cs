using System.ComponentModel.DataAnnotations;
using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Models.Api;
using Umbraco.Community.CSPManager.Services;

namespace Umbraco.Community.CSPManager.Tests.Models;

[TestFixture]
public class CspApiDefinitionValidationTests
{
	[Test]
	public void Validate_WithValidFrontEndId_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition { Id = Constants.DefaultFrontEndId };

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_WithValidBackOfficeId_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition { Id = Constants.DefaultBackofficeId };

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_WithInvalidId_ReturnsError()
	{
		var definition = new CspApiDefinition { Id = Guid.NewGuid() };

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.ErrorMessage, Is.EqualTo("Invalid Id"));
	}

	[TestCase("fac780be-53af-41dc-b51d-1aa647100221", TestName = "Frontend id with a DomainKey is invalid")]
	[TestCase("9cbfa28c-2b19-40f4-9f8e-bbc52bd8e780", TestName = "Backoffice id with a DomainKey is invalid")]
	public void Validate_GlobalIdWithDomainKey_ReturnsError(string globalId)
	{
		var definition = new CspApiDefinition { Id = Guid.Parse(globalId), DomainKey = Guid.NewGuid() };

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.MemberNames, Contains.Item(nameof(CspApiDefinition.DomainKey)));
	}

	[Test]
	public void Validate_NewDomainPolicy_WithEmptyId_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition { Id = Guid.Empty, DomainKey = Guid.NewGuid() };

		Assert.That(ValidateModel(definition), Is.Empty);
	}

	[Test]
	public void Validate_ExistingDomainPolicy_ReturnsNoErrors()
	{
		// Whether the (Id, DomainKey) pair matches the stored row is checked by the service.
		var definition = new CspApiDefinition { Id = Guid.NewGuid(), DomainKey = Guid.NewGuid() };

		Assert.That(ValidateModel(definition), Is.Empty);
	}

	[Test]
	public void Validate_DomainPolicyFlaggedAsBackOffice_ReturnsError()
	{
		var definition = new CspApiDefinition { Id = Guid.Empty, DomainKey = Guid.NewGuid(), IsBackOffice = true };

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.MemberNames, Contains.Item(nameof(CspApiDefinition.IsBackOffice)));
	}

	[Test]
	public void Validate_ReportUriDirective_WithAbsoluteHttpsUrl_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportUri,
			ReportUri = "https://example.com/csp-report"
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_ReportUriDirective_WithRelativeUrl_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportUri,
			ReportUri = "/api/csp-report"
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_ReportToDirective_WithEndpointName_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportTo,
			ReportUri = "csp-endpoint"
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_ReportUriDirective_WithFtpScheme_ReturnsError()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportUri,
			ReportUri = "ftp://example.com/report"
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.MemberNames, Contains.Item("ReportUri"));
	}

	[Test]
	public void Validate_ReportingDirective_WithMissingReportUri_ReturnsError()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportUri,
			ReportUri = null
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.ErrorMessage, Is.EqualTo("ReportUri is required when ReportingDirective is set"));
	}

	[Test]
	public void Validate_InvalidReportingDirective_ReturnsError()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = "invalid-directive",
			ReportUri = "https://example.com"
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.MemberNames, Contains.Item("ReportingDirective"));
	}

	[Test]
	public void Validate_DuplicateSources_ReturnsErrorWithSourceName()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new() { Source = "'self'", Directives = [Constants.Directives.DefaultSource] },
				new() { Source = "'self'", Directives = [Constants.Directives.ScriptSource] }
			]
		};

		var results = ValidateModel(definition);

		Assert.That(results.Any(r => r.ErrorMessage != null && r.ErrorMessage.Contains("Duplicate sources found: ''self''")), Is.True);
	}

	[Test]
	public void Validate_MultipleDuplicateSources_ListsAllDuplicates()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new() { Source = "'self'", Directives = [Constants.Directives.DefaultSource] },
				new() { Source = "https://example.com", Directives = [Constants.Directives.ScriptSource] },
				new() { Source = "'self'", Directives = [Constants.Directives.StyleSource] },
				new() { Source = "https://example.com", Directives = [Constants.Directives.ImageSource] }
			]
		};

		var results = ValidateModel(definition);
		var errorMessage = results.FirstOrDefault(r => r.MemberNames.Contains("Sources"))?.ErrorMessage;

		Assert.That(errorMessage, Is.Not.Null);
		Assert.Multiple(() =>
		{
			Assert.That(errorMessage, Does.Contain("'self'"));
			Assert.That(errorMessage, Does.Contain("https://example.com"));
		});
	}

	// CSP matching is case-insensitive and SQL Server's default collation would reject the
	// second row on save, so case variants must be reported as duplicates before that happens.
	[Test]
	public void Validate_DuplicateSourcesDifferingOnlyByCase_ReturnsError()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new() { Source = "https://Example.com", Directives = [Constants.Directives.ScriptSource] },
				new() { Source = "https://example.com", Directives = [Constants.Directives.StyleSource] }
			]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.ErrorMessage, Does.StartWith("Duplicate sources found"));
	}

	[TestCase("https://example.com 'unsafe-inline'", TestName = "Source with a space is rejected")]
	[TestCase("'self';script-src *", TestName = "Source with a semicolon is rejected")]
	[TestCase("a.example.com,b.example.com", TestName = "Source with a comma is rejected")]
	[TestCase("https://example.com\r\nX-Injected: 1", TestName = "Source with a line break is rejected")]
	[TestCase("https://example.com\t", TestName = "Source with a tab is rejected")]
	public void Validate_SourceThatIsNotASingleToken_ReturnsError(string source)
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources = [new() { Source = source, Directives = [Constants.Directives.DefaultSource] }]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(results.FirstOrDefault()?.ErrorMessage, Does.Contain("must be a single token"));
			Assert.That(results.FirstOrDefault()?.MemberNames, Contains.Item("Sources"));
		});
	}

	[TestCase("'self'")]
	[TestCase("https://cdn.example.com/path/to/file.js")]
	[TestCase("*.example.com")]
	[TestCase("data:")]
	[TestCase("'sha256-abc+def/ghi=='")]
	[TestCase("'nonce-r4nd0m'")]
	public void Validate_SingleTokenSource_ReturnsNoErrors(string source)
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources = [new() { Source = source, Directives = [Constants.Directives.DefaultSource] }]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	// The service strips whitespace-only sources on save, so the empty row the UI adds must not fail validation.
	[Test]
	public void Validate_EmptySource_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources = [new() { Source = "", Directives = [] }]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_UnknownDirective_ReturnsError()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new() { Source = "'self'", Directives = ["not-a-real-directive"] }
			]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.ErrorMessage, Does.Contain("Unknown directive"));
	}

	[Test]
	public void Validate_ValidDirectives_ReturnsNoErrors()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new()
				{
					Source = "'self'",
					Directives =
					[
						Constants.Directives.DefaultSource,
						Constants.Directives.ScriptSource,
						Constants.Directives.StyleSource
					]
				}
			]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Is.Empty);
	}

	[Test]
	public void Validate_SourceExceedsMaxLength_ReturnsError()
	{
		var longSource = new string('a', 4001);
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources =
			[
				new() { Source = longSource, Directives = [Constants.Directives.DefaultSource] }
			]
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.That(results.FirstOrDefault()?.ErrorMessage, Does.Contain("exceeds maximum length"));
	}

	[TestCase("csp-endpoint")]
	[TestCase("default")]
	[TestCase("csp_endpoint.v2")]
	public void Validate_ReportToDirective_WithTokenEndpointName_ReturnsNoErrors(string endpoint)
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportTo,
			ReportUri = endpoint
		};

		Assert.That(ValidateModel(definition), Is.Empty);
	}

	[TestCase("https://example.com/report", TestName = "report-to with a URL instead of an endpoint name is rejected")]
	[TestCase("csp(endpoint)", TestName = "report-to with a non-token character is rejected")]
	[TestCase("endpöint", TestName = "report-to with a non-ASCII character is rejected")]
	public void Validate_ReportToDirective_WithInvalidEndpointName_ReturnsError(string endpoint)
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportTo,
			ReportUri = endpoint
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(results[0].ErrorMessage, Does.Contain("endpoint name"));
			Assert.That(results[0].MemberNames, Contains.Item("ReportUri"));
		});
	}

	[TestCase("report-uri", "https://example.com/report; script-src *", TestName = "report-uri with a semicolon is rejected")]
	[TestCase("report-uri", "https://example.com/report\r\nX-Injected: 1", TestName = "report-uri with a line break is rejected")]
	[TestCase("report-uri", "/report a", TestName = "report-uri with a space is rejected")]
	[TestCase("report-to", "csp-endpoint, other", TestName = "report-to with a comma is rejected")]
	public void Validate_ReportUriThatIsNotASingleValue_ReturnsError(string directive, string reportUri)
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = directive,
			ReportUri = reportUri
		};

		var results = ValidateModel(definition);

		Assert.That(results, Has.Count.EqualTo(1));
		Assert.Multiple(() =>
		{
			Assert.That(results[0].ErrorMessage, Does.Contain("must be a single value"));
			Assert.That(results[0].MemberNames, Contains.Item("ReportUri"));
		});
	}

	[Test]
	public void Validate_ErrorMessages_MaskControlCharacters()
	{
		var definition = new CspApiDefinition
		{
			Id = Constants.DefaultFrontEndId,
			Sources = [new() { Source = "a\r\nb", Directives = [Constants.Directives.DefaultSource] }]
		};

		var results = ValidateModel(definition);

		Assert.That(results.Single().ErrorMessage, Does.Contain("'a??b'").And.Not.Contain("\n"));
	}

	// The service applies the same rules to a CspDefinition, for callers that bypass the API.
	[Test]
	public void CspDefinitionValidator_AppliesTheSameRulesToAStoredDefinition()
	{
		var results = CspDefinitionValidator.Validate(new CspDefinition
		{
			Id = Constants.DefaultFrontEndId,
			ReportingDirective = Constants.ReportingDirectives.ReportTo,
			ReportUri = "a b",
			Sources = [new CspDefinitionSource { Source = "x;script-src", Directives = ["not-a-directive"] }]
		});

		Assert.That(results.Select(r => r.ErrorMessage), Has.Some.Contains("must be a single value")
			.And.Some.Contains("must be a single token")
			.And.Some.Contains("Unknown directive"));
	}

	private static List<ValidationResult> ValidateModel(CspApiDefinition definition)
	{
		var validationContext = new ValidationContext(definition);
		var results = new List<ValidationResult>();
		Validator.TryValidateObject(definition, validationContext, results, validateAllProperties: true);
		return results;
	}
}