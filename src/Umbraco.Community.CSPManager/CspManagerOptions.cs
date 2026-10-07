namespace Umbraco.Community.CSPManager;

/// <summary>
/// Options for CSP Manager, bound from the <c>CspManager</c> configuration section.
/// </summary>
public sealed class CspManagerOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether the CSP header is left off backoffice responses.
	/// </summary>
	public bool DisableBackOfficeHeader { get; set; } = false;

	/// <summary>
	/// Gets or sets what happens on a request for a domain whose domain policy is disabled.
	/// Defaults to <see cref="DisabledDomainPolicyBehavior.FallbackToGlobal"/>.
	/// </summary>
	/// <remarks>
	/// A domain without a domain policy always uses the global frontend policy; this setting only
	/// applies when a domain policy exists but is switched off.
	/// </remarks>
	public DisabledDomainPolicyBehavior DisabledDomainPolicyBehavior { get; set; } = DisabledDomainPolicyBehavior.FallbackToGlobal;
}

/// <summary>
/// What the middleware does when the domain policy for the requested domain is disabled.
/// </summary>
public enum DisabledDomainPolicyBehavior
{
	/// <summary>
	/// Apply the global frontend policy instead, as if the domain had no policy. A disabled domain
	/// policy never removes protection the rest of the site has.
	/// </summary>
	FallbackToGlobal = 0,

	/// <summary>
	/// Send no CSP header for that domain, the same way a disabled global policy sends none.
	/// </summary>
	NoHeader = 1,
}
