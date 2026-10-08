using Umbraco.Community.CSPManager.Models;

namespace Umbraco.Community.CSPManager.Tests.Models;

[TestFixture]
public class CspDomainKeyTests
{
	[TestCase("Example.COM", TestName = "Case is ignored")]
	[TestCase("  example.com ", TestName = "Surrounding whitespace is ignored")]
	public void FromDomainName_NormalisesTheName(string variant)
		=> Assert.That(CspDomainKey.FromDomainName(variant), Is.EqualTo(CspDomainKey.FromDomainName("example.com")));

	[TestCase("example.com", "example.com/en")]
	[TestCase("example.com", "www.example.com")]
	[TestCase("a.example.com", "b.example.com")]
	public void FromDomainName_DiffersForDifferentDomains(string first, string second)
		=> Assert.That(CspDomainKey.FromDomainName(first), Is.Not.EqualTo(CspDomainKey.FromDomainName(second)));

	// Umbraco regenerates IDomain.Key on every load, so the key comes from the name alone. Pinned to a
	// value computed independently (SHA-256 over namespace + name, RFC 9562 v8 bits): changing the
	// derivation would orphan every stored domain policy.
	[Test]
	public void FromDomainName_HasNotChanged()
		=> Assert.That(CspDomainKey.FromDomainName("example.com"), Is.EqualTo(new Guid("de9246d9-2cd0-842c-8b5e-87c5c00a4ff0")));
}
