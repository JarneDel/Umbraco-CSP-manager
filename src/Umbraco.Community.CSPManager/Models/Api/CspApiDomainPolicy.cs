namespace Umbraco.Community.CSPManager.Models.Api;

/// <summary>
/// Summary of a domain policy, as listed under the Frontend policy in the backoffice tree.
/// </summary>
public sealed class CspApiDomainPolicy
{
	/// <summary>
	/// Gets or sets the id of the domain policy.
	/// </summary>
	public Guid Id { get; set; }

	/// <summary>
	/// Gets or sets the key of the domain the policy applies to (see <see cref="CspDomainKey.FromDomainName"/>).
	/// </summary>
	public Guid DomainKey { get; set; }

	/// <summary>
	/// Gets or sets the domain name, or <c>null</c> when the domain no longer exists.
	/// </summary>
	public string? DomainName { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the policy is enabled.
	/// </summary>
	public bool Enabled { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the associated domain was removed or renamed in Umbraco.
	/// </summary>
	public bool IsOrphaned { get; set; }
}
