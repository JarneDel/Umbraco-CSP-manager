using System.Security.Cryptography;
using System.Text;

namespace Umbraco.Community.CSPManager.Models;

/// <summary>
/// Utility for generating deterministic keys for domain policies based on domain hostnames.
/// </summary>
/// <remarks>
/// Generates a normalized, namespace-hashed UUID (v8) from the domain name to ensure consistent identification across environments.
/// </remarks>
public static class CspDomainKey
{
	// Fixed namespace so these keys can't collide with name-based UUIDs derived for other purposes.
	private static readonly Guid Namespace = new("7c3b1f0e-5a2d-4e8b-9f61-2d4a8c0e6b13");

	/// <summary>
	/// Gets the domain policy key for an Umbraco domain name, e.g. <c>example.com</c> or <c>example.com/en</c>.
	/// </summary>
	/// <param name="domainName">The domain name as configured in Culture &amp; Hostnames.</param>
	/// <returns>The key; equal for names that differ only by case or surrounding whitespace.</returns>
	public static Guid FromDomainName(string domainName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(domainName);

		var name = Encoding.UTF8.GetBytes(domainName.Trim().ToLowerInvariant());
		var input = new byte[16 + name.Length];
		Namespace.TryWriteBytes(input, bigEndian: true, out _);
		name.CopyTo(input, 16);

		Span<byte> hash = stackalloc byte[32];
		SHA256.HashData(input, hash);

		// RFC 9562: version 8 (custom, name-based here) and the RFC variant bits.
		hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
		hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

		return new Guid(hash[..16], bigEndian: true);
	}
}
