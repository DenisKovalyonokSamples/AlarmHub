using AlarmHub.Api.Domain.Panels;

namespace AlarmHub.UnitTests.Api;

/// <summary>
/// Credential handling for physical panels.
/// </summary>
/// <remarks>
/// These tests protect the security contract: the plaintext key is never stored, verification is
/// exact, and a wrong key never passes.
/// </remarks>
public sealed class PanelApiKeysTests
{
    /// <summary>A generated key verifies against its own hash and salt.</summary>
    [Fact]
    public void Verify_WithCorrectKey_ReturnsTrue()
    {
        (string plaintext, string hash, string salt) = PanelApiKeys.Create();

        Assert.True(PanelApiKeys.Verify(plaintext, hash, salt));
    }

    /// <summary>Any other key must fail.</summary>
    [Fact]
    public void Verify_WithWrongKey_ReturnsFalse()
    {
        (_, string hash, string salt) = PanelApiKeys.Create();

        Assert.False(PanelApiKeys.Verify("not-the-key", hash, salt));
    }

    /// <summary>An empty key must fail without throwing.</summary>
    [Fact]
    public void Verify_WithEmptyKey_ReturnsFalse()
    {
        (_, string hash, string salt) = PanelApiKeys.Create();

        Assert.False(PanelApiKeys.Verify(string.Empty, hash, salt));
    }

    /// <summary>Keys are unique per panel, otherwise a leak would compromise every device.</summary>
    [Fact]
    public void Create_TwoCalls_ProduceDifferentKeys()
    {
        var (first, firstHash, _) = PanelApiKeys.Create();
        var (second, secondHash, _) = PanelApiKeys.Create();

        Assert.NotEqual(first, second);
        Assert.NotEqual(firstHash, secondHash);
    }

    /// <summary>The stored hash must not be the plaintext key.</summary>
    [Fact]
    public void Create_DoesNotReturnPlaintextAsHash()
    {
        (string plaintext, string hash, _) = PanelApiKeys.Create();

        Assert.NotEqual(plaintext, hash);
    }
}
