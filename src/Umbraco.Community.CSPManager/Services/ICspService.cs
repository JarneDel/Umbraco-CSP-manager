using Microsoft.AspNetCore.Http;
using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.Services;

/// <summary>
/// Service for managing Content Security Policy (CSP) definitions and nonce generation.
/// </summary>
/// <remarks>
/// This service provides methods to retrieve, save, and cache CSP definitions for both
/// frontend and backoffice contexts. It also handles cryptographically secure nonce
/// generation for script and style elements.
/// </remarks>
public interface ICspService
{
	/// <summary>
	/// Retrieves the CSP definition from the database for the specified context.
	/// </summary>
	/// <param name="isBackOfficeRequest">
	/// <c>true</c> to retrieve the backoffice CSP policy; <c>false</c> for the frontend policy.
	/// </param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>
	/// The <see cref="CspDefinition"/> for the specified context, or a default disabled
	/// definition if none exists in the database.
	/// </returns>
	/// <remarks>Only returns a global policy; domain policies are never returned here.</remarks>
	Task<CspDefinition> GetCspDefinitionAsync(bool isBackOfficeRequest, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves a CSP definition (global or domain policy) from the database by its id.
	/// </summary>
	/// <param name="key">The definition id.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The definition, or <c>null</c> if there is no definition with that id.</returns>
	Task<CspDefinition?> GetCspDefinitionAsync(Guid key, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the domain policy for an Umbraco domain from the database.
	/// </summary>
	/// <param name="domainKey">The domain key, see <see cref="CspDomainKey.FromDomainName"/>.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The domain policy, or <c>null</c> if the domain has none.</returns>
	Task<CspDefinition?> GetCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the domain policy for an Umbraco domain from the runtime cache, loading it from the
	/// database if it isn't cached.
	/// </summary>
	/// <param name="domainKey">The domain key, see <see cref="CspDomainKey.FromDomainName"/>.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>
	/// A defensive copy of the cached domain policy, or <c>null</c> if the domain has none. The
	/// absence of a policy is cached too, until a policy for the domain is saved.
	/// </returns>
	Task<CspDefinition?> GetCachedCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves every domain policy, including orphaned ones whose domain has since been removed.
	/// </summary>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The domain policies with their sources.</returns>
	Task<List<CspDefinition>> GetAllDomainPoliciesAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Creates a domain policy for an Umbraco domain as a copy of the global frontend policy.
	/// </summary>
	/// <param name="domainKey">The key (see <see cref="CspDomainKey.FromDomainName"/>) of an existing, non-wildcard Umbraco domain.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The saved domain policy.</returns>
	/// <exception cref="CspDefinitionValidationException">
	/// The domain doesn't exist, is a wildcard domain, or already has a policy.
	/// </exception>
	Task<CspDefinition> CreateCspDefinitionForDomainAsync(Guid domainKey, CancellationToken cancellationToken);

	/// <summary>
	/// Deletes a domain policy and publishes a <see cref="Notifications.CspDeletedNotification"/>.
	/// Does nothing if there is no definition with that id.
	/// </summary>
	/// <param name="id">The id of the domain policy.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <exception cref="CspDefinitionValidationException">The id is one of the two global policies.</exception>
	Task DeleteCspDefinitionAsync(Guid id, CancellationToken cancellationToken);

	/// <summary>
	/// Retrieves the CSP definition from the runtime cache, loading from the database if not cached.
	/// </summary>
	/// <param name="isBackOfficeRequest">
	/// <c>true</c> to retrieve the backoffice CSP policy; <c>false</c> for the frontend policy.
	/// </param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>
	/// The cached <see cref="CspDefinition"/> for the specified context, or <c>null</c> if
	/// the cache factory returns null.
	/// </returns>
	/// <remarks>
	/// This method uses separate cache keys for frontend and backoffice policies to ensure
	/// isolation. The cache is automatically invalidated when definitions are saved.
	/// </remarks>
	Task<CspDefinition?> GetCachedCspDefinitionAsync(bool isBackOfficeRequest, CancellationToken cancellationToken);

	/// <summary>
	/// Saves a CSP definition to the database and publishes a <see cref="Notifications.CspSavedNotification"/>.
	/// </summary>
	/// <param name="definition">The CSP definition to save.</param>
	/// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
	/// <returns>The saved <see cref="CspDefinition"/> with any modifications applied during save.</returns>
	/// <remarks>
	/// <para>
	/// This method removes empty or whitespace-only sources before saving. It also deletes
	/// any sources that were removed from the definition and publishes a notification
	/// that can be handled by other components.
	/// </para>
	/// <para>
	/// The service enforces identity: the two global ids can never carry a
	/// <see cref="CspDefinition.DomainKey"/>; any other id must carry one; an existing domain
	/// policy keeps its domain; a new domain policy needs an existing non-wildcard domain without a
	/// policy. Domain policies are always frontend policies, and sources always belong to the
	/// definition being saved. A domain policy saved with <see cref="Guid.Empty"/> as its id is
	/// created with a new id assigned by the service.
	/// </para>
	/// </remarks>
	/// <exception cref="CspDefinitionValidationException">The definition breaks one of these rules.</exception>
	Task<CspDefinition> SaveCspDefinitionAsync(CspDefinition definition, CancellationToken cancellationToken);

	/// <summary>
	/// Gets or creates a cryptographically secure nonce for use in CSP directives.
	/// </summary>
	/// <param name="context">The current HTTP context.</param>
	/// <returns>
	/// A Base64-encoded 128-bit nonce value, or an empty string if the context is unavailable.
	/// </returns>
	/// <remarks>
	/// The nonce is generated using <see cref="System.Security.Cryptography.RandomNumberGenerator"/>
	/// and is stored in the HTTP context for reuse within the same request. The same nonce value
	/// is used for both script-src and style-src directives.
	/// </remarks>
	string GetOrCreateCspNonce(HttpContext context);
}