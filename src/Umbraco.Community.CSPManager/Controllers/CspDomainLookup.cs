using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.Controllers;

/// <summary>
/// Reads the non-wildcard Umbraco domains with the content node each is assigned to, for the
/// backoffice API. Not cached: it serves backoffice requests only, which are rare.
/// </summary>
internal sealed class CspDomainLookup
{
	private readonly IDomainService _domainService;
	private readonly IEntityService _entityService;

	public CspDomainLookup(IDomainService domainService, IEntityService entityService)
	{
		_domainService = domainService;
		_entityService = entityService;
	}

	public async Task<IReadOnlyDictionary<Guid, DomainDetails>> GetDomainsAsync()
	{
		var domains = (await _domainService.GetAllAsync(includeWildcards: false)).ToList();

		var rootIds = domains
			.Where(d => d.RootContentId.HasValue)
			.Select(d => d.RootContentId!.Value)
			.Distinct()
			.ToArray();
		var roots = rootIds.Length == 0
			? new Dictionary<int, Umbraco.Cms.Core.Models.Entities.IEntitySlim>()
			: _entityService.GetAll(UmbracoObjectTypes.Document, rootIds).ToDictionary(e => e.Id);

		var result = new Dictionary<Guid, DomainDetails>();
		foreach (var domain in domains.Where(d => !string.IsNullOrWhiteSpace(d.DomainName)).OrderBy(d => d.SortOrder))
		{
			var root = domain.RootContentId is { } rootId && roots.TryGetValue(rootId, out var entity) ? entity : null;
			// IDomain.Key isn't persisted by Umbraco; policies use the name-derived key.
			result[CspDomainKey.FromDomainName(domain.DomainName)] = new DomainDetails(
				domain.DomainName,
				domain.LanguageIsoCode,
				root?.Key,
				root?.Name);
		}

		return result;
	}

	public async Task<DomainDetails?> GetDomainAsync(Guid? domainKey)
	{
		if (domainKey is null)
		{
			return null;
		}

		var domains = await GetDomainsAsync();
		return domains.TryGetValue(domainKey.Value, out var details) ? details : null;
	}

	internal sealed record DomainDetails(string Name, string? Culture, Guid? RootContentKey, string? RootContentName);
}
