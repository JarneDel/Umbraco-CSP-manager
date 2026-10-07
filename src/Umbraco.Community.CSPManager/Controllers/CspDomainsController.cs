using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.CSPManager.Models.Api;
using Umbraco.Community.CSPManager.Services;

namespace Umbraco.Community.CSPManager.Controllers;

/// <summary>
/// API controller listing the Umbraco domains a domain policy can be created for. Not named
/// <c>DomainsController</c>: Umbraco has one, and controller names must be unique.
/// </summary>
[ApiVersion("1.0")]
[ApiExplorerSettings(GroupName = "Domains")]
public class CspDomainsController : CspManagerControllerBase
{
	private readonly ICspService _cspService;
	private readonly CspDomainLookup _domainLookup;

	/// <summary>
	/// Initializes a new instance of the <see cref="CspDomainsController"/> class.
	/// </summary>
	/// <param name="cspService">The CSP service, to find which domains have a policy.</param>
	/// <param name="domainService">The Umbraco domain service.</param>
	/// <param name="entityService">The Umbraco entity service, to resolve the content a domain is assigned to.</param>
	public CspDomainsController(ICspService cspService, IDomainService domainService, IEntityService entityService)
	{
		_cspService = cspService;
		_domainLookup = new CspDomainLookup(domainService, entityService);
	}

	/// <summary>
	/// Lists every non-wildcard Umbraco domain with the content node it's assigned to and whether
	/// it already has a domain policy.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The domains, in Umbraco's sort order.</returns>
	[HttpGet("Domains")]
	[MapToApiVersion("1.0")]
	[ProducesResponseType(typeof(IEnumerable<CspDomainInfo>), 200)]
	public async Task<ActionResult<IEnumerable<CspDomainInfo>>> GetDomains(CancellationToken cancellationToken = default)
	{
		var domains = await _domainLookup.GetDomainsAsync();
		var policies = (await _cspService.GetAllDomainPoliciesAsync(cancellationToken))
			.ToDictionary(p => p.DomainKey!.Value, p => p.Id);

		var result = domains.Select(d => new CspDomainInfo
		{
			Key = d.Key,
			Name = d.Value.Name,
			Culture = d.Value.Culture,
			RootContentKey = d.Value.RootContentKey,
			RootContentName = d.Value.RootContentName,
			HasCspPolicy = policies.ContainsKey(d.Key),
			CspDefinitionId = policies.TryGetValue(d.Key, out var id) ? id : null,
		}).ToList();

		return Ok(result);
	}
}
