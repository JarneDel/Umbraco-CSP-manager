using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.uSync;

/// <summary>
/// Provides alias and naming helpers for uSync CSP definitions.
/// </summary>
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
