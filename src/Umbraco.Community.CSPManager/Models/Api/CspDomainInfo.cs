namespace Umbraco.Community.CSPManager.Models.Api;

/// <summary>
/// An Umbraco domain (Culture &amp; Hostnames, non-wildcard) and whether it has a domain policy.
/// </summary>
public sealed class CspDomainInfo
{
	/// <summary>
	/// Gets or sets the domain key (see <see cref="CspDomainKey.FromDomainName"/>).
	/// </summary>
	public Guid Key { get; set; }

	/// <summary>
	/// Gets or sets the domain name, e.g. <c>example.com</c> or <c>example.com/en</c>.
	/// </summary>
	public string Name { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the culture (ISO code) assigned to the domain, if any.
	/// </summary>
	public string? Culture { get; set; }

	/// <summary>
	/// Gets or sets the key of the content node the domain is assigned to.
	/// </summary>
	public Guid? RootContentKey { get; set; }

	/// <summary>
	/// Gets or sets the name of the content node the domain is assigned to.
	/// </summary>
	public string? RootContentName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the domain already has a domain policy.
	/// </summary>
	public bool HasCspPolicy { get; set; }

	/// <summary>
	/// Gets or sets the id of the domain's policy, if it has one.
	/// </summary>
	public Guid? CspDefinitionId { get; set; }
}
