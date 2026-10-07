using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.uSync;

/// <summary>
/// Names and aliases uSync uses for CSP definitions.
/// </summary>
/// <remarks>
/// Domain policies are named after the domain key rather than the hostname: the key is what
/// matches a domain across environments (uSync syncs domains by key), a hostname often differs
/// per environment, and a name derived from it would rename the uSync file whenever it changes.
/// </remarks>
internal static class CspItemNames
{
	public const string BackOfficeAlias = "backoffice";

	public const string FrontEndAlias = "front-end";

	public const string DomainAliasPrefix = "domain-";

	public static string Alias(CspDefinition definition)
		=> definition.DomainKey is { } domainKey
			? $"{DomainAliasPrefix}{domainKey:D}"
			: definition.IsBackOffice ? BackOfficeAlias : FrontEndAlias;

	public static string Name(CspDefinition definition)
		=> definition.DomainKey is { } domainKey
			? $"Domain {domainKey:D}"
			: definition.IsBackOffice ? "Backoffice" : "Frontend";
}
