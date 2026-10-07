using Microsoft.AspNetCore.Http;
using Umbraco.Community.CSPManager.Models;
using Umbraco.Community.CSPManager.Services;

namespace Umbraco.Community.CSPManager.uSync.Tests;

/// <summary>
/// In-memory <see cref="ICspService"/>: enough of the real rules for the serializer tests.
/// </summary>
internal sealed class FakeCspService : ICspService
{
	public Dictionary<Guid, CspDefinition> Definitions { get; } = [];

	public List<Guid> Deleted { get; } = [];

	public Task<CspDefinition> GetCspDefinitionAsync(bool isBackOfficeRequest, CancellationToken cancellationToken)
	{
		var id = isBackOfficeRequest ? Constants.DefaultBackofficeId : Constants.DefaultFrontEndId;
		return Task.FromResult(Definitions.TryGetValue(id, out var definition)
			? definition
			: new CspDefinition { Id = id, IsBackOffice = isBackOfficeRequest });
	}

	public Task<CspDefinition?> GetCspDefinitionAsync(Guid key, CancellationToken cancellationToken)
		=> Task.FromResult(Definitions.GetValueOrDefault(key));

	public Task<CspDefinition?> GetCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken)
		=> Task.FromResult(Definitions.Values.FirstOrDefault(d => d.DomainKey == domainKey));

	public Task<CspDefinition?> GetCachedCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken)
		=> GetCspDefinitionForDomainAsync(domainKey, cancellationToken);

	public Task<List<CspDefinition>> GetAllDomainPoliciesAsync(CancellationToken cancellationToken)
		=> Task.FromResult(Definitions.Values.Where(d => d.DomainKey is not null).ToList());

	public Task<CspDefinition> CreateCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken)
		=> throw new NotSupportedException();

	public Task DeleteCspDefinitionAsync(Guid id, CancellationToken cancellationToken)
	{
		Deleted.Add(id);
		Definitions.Remove(id);
		return Task.CompletedTask;
	}

	public async Task<CspDefinition?> GetCachedCspDefinitionAsync(bool isBackOfficeRequest, CancellationToken cancellationToken)
		=> await GetCspDefinitionAsync(isBackOfficeRequest, cancellationToken);

	public Task<CspDefinition> SaveCspDefinitionAsync(CspDefinition definition, CancellationToken cancellationToken)
	{
		Definitions[definition.Id] = definition;
		return Task.FromResult(definition);
	}

	public string GetOrCreateCspNonce(HttpContext context) => string.Empty;
}
