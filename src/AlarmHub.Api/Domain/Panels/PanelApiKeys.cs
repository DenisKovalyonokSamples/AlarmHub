using System.Security.Cryptography;
using System.Text;

namespace AlarmHub.Api.Domain.Panels;

/// <summary>
/// Creates and verifies the credential a physical panel presents when it posts telemetry.
/// </summary>
/// <remarks>
/// Only the salted hash is persisted, so a database leak does not expose working device
/// credentials. The plaintext key is shown to the installer exactly once, at registration.
/// SHA-256 is sufficient here: the key itself is 256 bits of randomness, so a slow password
/// hash would add latency to every ingestion request without improving security.
/// </remarks>
public static class PanelApiKeys
{
    /// <summary>
    /// Generates a new credential for a panel.
    /// </summary>
    /// <returns>
    /// The plaintext key to hand to the device, plus the hash and salt that must be stored on the aggregate.
    /// </returns>
    public static (string PlaintextKey, string Hash, string Salt) Create()
    {
        string plaintextKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        string salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        return (plaintextKey, ComputeHash(plaintextKey, salt), salt);
    }

    /// <summary>
    /// Verifies a key presented by a panel.
    /// </summary>
    /// <param name="presentedKey">Plaintext key taken from the request header.</param>
    /// <param name="expectedHash">Hash stored on the panel aggregate.</param>
    /// <param name="salt">Salt stored on the panel aggregate.</param>
    /// <returns>True when the key matches; comparison is fixed-time.</returns>
    public static bool Verify(string presentedKey, string expectedHash, string salt)
    {
        if (string.IsNullOrEmpty(presentedKey))
        {
            return false;
        }

        byte[] expected = Convert.FromBase64String(expectedHash);
        byte[] actual = Convert.FromBase64String(ComputeHash(presentedKey, salt));

        // Fixed-time comparison keeps the ingest endpoint from leaking information through timing.
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Computes the salted SHA-256 hash of a plaintext key.</summary>
    /// <param name="plaintextKey">Key to hash.</param>
    /// <param name="salt">Per-panel salt.</param>
    private static string ComputeHash(string plaintextKey, string salt)
    {
        byte[] input = Encoding.UTF8.GetBytes(salt + ":" + plaintextKey);
        return Convert.ToBase64String(SHA256.HashData(input));
    }
}
