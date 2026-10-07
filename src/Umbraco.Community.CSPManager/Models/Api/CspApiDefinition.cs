using System.ComponentModel.DataAnnotations;
using Umbraco.Community.CSPManager.Services;

namespace Umbraco.Community.CSPManager.Models.Api;

/// <summary>
/// API data transfer object representing a Content Security Policy definition.
/// </summary>
/// <remarks>
/// This class is used for API requests and responses. It includes validation logic
/// to ensure the definition has a valid ID and no duplicate sources.
/// </remarks>
public sealed class CspApiDefinition : IValidatableObject
{
	/// <summary>
	/// Gets or sets the unique identifier for this CSP definition.
	/// Either <see cref="Constants.DefaultFrontEndId"/> or <see cref="Constants.DefaultBackofficeId"/>
	/// for the global policies, or the id the server gave a domain policy when it was created.
	/// Post <see cref="Guid.Empty"/> with a <see cref="DomainKey"/> to create a domain policy.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this CSP policy is enabled.
	/// When <c>false</c>, no CSP header will be added to responses.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use report-only mode.
	/// When <c>true</c>, uses the Content-Security-Policy-Report-Only header instead of Content-Security-Policy.
	/// </summary>
	public bool ReportOnly { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this definition is for the Umbraco backoffice.
	/// </summary>
	public bool IsBackOffice { get; set; }

	/// <summary>
	/// Gets or sets the key of the Umbraco domain this policy applies to (see
	/// <see cref="CspDomainKey.FromDomainName"/>), or <c>null</c> for the two
	/// global policies. It can't be changed once a domain policy exists.
	/// </summary>
	public Guid? DomainKey { get; set; }

	/// <summary>
	/// Gets or sets the name of the domain, for display only (ignored on save). <c>null</c> for the
	/// global policies, and for a domain policy whose domain has been removed.
	/// </summary>
	public string? DomainName { get; set; }

	/// <summary>
	/// Gets or sets the key of the content node the domain is assigned to, for display only
	/// (ignored on save). <c>null</c> for the global policies.
	/// </summary>
	public Guid? RootContentKey { get; set; }

	/// <summary>
	/// Gets or sets what happens when this domain policy is disabled (the configured
	/// <see cref="CspManagerOptions.DisabledDomainPolicyBehavior"/>), for display only (ignored on
	/// save). <c>null</c> for the global policies.
	/// </summary>
	public DisabledDomainPolicyBehavior? DisabledDomainPolicyBehavior { get; set; }

	/// <summary>
	/// Gets or sets the reporting directive to use (e.g., "report-uri" or "report-to").
	/// </summary>
	public string? ReportingDirective { get; set; }

	/// <summary>
	/// Gets or sets the URI where CSP violation reports should be sent.
	/// </summary>
	public string? ReportUri { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to include the upgrade-insecure-requests directive.
	/// When <c>true</c>, browsers will upgrade HTTP requests to HTTPS.
	/// </summary>
	public bool UpgradeInsecureRequests { get; set; }

	/// <summary>
	/// Gets or sets the list of CSP sources and their associated directives.
	/// </summary>
	public List<CspApiDefinitionSource> Sources { get; set; } = [];

	/// <summary>
	/// Validates the CSP definition according to CSP specification rules.
	/// </summary>
	/// <param name="validationContext">The validation context.</param>
	/// <returns>A collection of validation results.</returns>
	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		var isGlobalId = Constants.DefaultFrontEndId.Equals(Id) || Constants.DefaultBackofficeId.Equals(Id);

		// The service enforces the same rules (and checks the stored row); this just fails fast.
		if (DomainKey is null)
		{
			// Without a domain, only the two global policies exist.
			if (!isGlobalId)
			{
				yield return new ValidationResult("Invalid Id", [nameof(Id)]);
			}
		}
		else
		{
			if (isGlobalId)
			{
				yield return new ValidationResult("The global frontend and backoffice policies cannot be assigned to a domain", [nameof(DomainKey)]);
			}

			if (IsBackOffice)
			{
				yield return new ValidationResult("Domain policies cannot be backoffice policies", [nameof(IsBackOffice)]);
			}
		}

		// The header content rules are shared with the service, which enforces them for every
		// caller (uSync, custom code); validating here too keeps them as model-state errors.
		foreach (var result in CspDefinitionValidator.Validate(
			ReportingDirective,
			ReportUri,
			[.. Sources.Select(s => (s.Source, (IReadOnlyCollection<string>)s.Directives))]))
		{
			yield return result;
		}
	}

	internal static CspApiDefinition FromCspDefinition(
		CspDefinition definition,
		string? domainName = null,
		DisabledDomainPolicyBehavior? disabledDomainPolicyBehavior = null,
		Guid? rootContentKey = null)
		=> new()
		{
			Id = definition.Id,
			DomainKey = definition.DomainKey,
			DomainName = definition.DomainKey is null ? null : domainName,
			RootContentKey = definition.DomainKey is null ? null : rootContentKey,
			DisabledDomainPolicyBehavior = definition.DomainKey is null ? null : disabledDomainPolicyBehavior,
			Enabled = definition.Enabled,
			UpgradeInsecureRequests = definition.UpgradeInsecureRequests,
			ReportingDirective = definition.ReportingDirective,
			IsBackOffice = definition.IsBackOffice,
			ReportOnly = definition.ReportOnly,
			ReportUri = definition.ReportUri,
			Sources = definition.Sources.ConvertAll(CspApiDefinitionSource.FromCspDefinitionSource),
		};

	internal CspDefinition ToCspDefinition()
		=> new()
		{
			Id = Id,
			Enabled = Enabled,
			UpgradeInsecureRequests = UpgradeInsecureRequests,
			ReportOnly = ReportOnly,
			IsBackOffice = IsBackOffice,
			ReportingDirective = ReportingDirective,
			ReportUri = ReportUri,
			DomainKey = DomainKey,
			Sources = Sources.ConvertAll(CspApiDefinitionSource.ToCspDefinitionSource)
		};
}