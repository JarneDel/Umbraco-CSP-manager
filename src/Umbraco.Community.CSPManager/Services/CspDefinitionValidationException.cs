namespace Umbraco.Community.CSPManager.Services;

/// <summary>
/// Thrown by <see cref="ICspService"/> when a save or delete would break the rules the service
/// enforces on CSP definition identity, regardless of who calls it (API, uSync, custom code).
/// </summary>
/// <remarks>
/// Examples: a global policy carrying a <c>DomainKey</c>, re-targeting an existing domain policy to
/// another domain, a second policy for the same domain, a domain that doesn't exist, or deleting
/// one of the two global policies.
/// </remarks>
public sealed class CspDefinitionValidationException : InvalidOperationException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="CspDefinitionValidationException"/> class.
	/// </summary>
	/// <param name="memberName">The <see cref="Models.CspDefinition"/> member the error relates to.</param>
	/// <param name="message">A message that describes the error.</param>
	public CspDefinitionValidationException(string memberName, string message) : base(message)
	{
		MemberName = memberName;
	}

	/// <summary>
	/// Gets the <see cref="Models.CspDefinition"/> member the error relates to, e.g. <c>DomainKey</c>.
	/// </summary>
	public string MemberName { get; }
}
