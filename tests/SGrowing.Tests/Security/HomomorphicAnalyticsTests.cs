using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Security.HomomorphicAnalytics;
using Xunit;

namespace SGrowing.Tests.Security;

public sealed class HomomorphicAnalyticsTests
{
    private readonly PaillierHomomorphicAnalyticsEngine _engine = new();

    [Fact]
    public void PaillierEncryptionAndDecryption_RecoversExactPlaintext()
    {
        // Arrange (Using 128-bit key for sub-100ms test execution)
        var (pub, priv) = _engine.GenerateKeyPair(bitLength: 128);
        long originalValue = 42500;

        // Act
        var ciphertext = _engine.Encrypt(originalValue, pub);
        var decrypted = _engine.Decrypt(ciphertext, priv);

        // Assert
        ciphertext.Should().NotBe(originalValue);
        decrypted.Should().Be(originalValue);
    }

    [Fact]
    public void HomomorphicAddition_SumOfCiphertexts_DecryptsToExactSum()
    {
        // Arrange: 3 confidential regional sales revenue data points
        var (pub, priv) = _engine.GenerateKeyPair(bitLength: 128);
        long salesAlgiers = 150000;
        long salesOran = 95000;
        long salesConstantine = 80000;

        // Act: Encrypt individual data points
        var c1 = _engine.Encrypt(salesAlgiers, pub);
        var c2 = _engine.Encrypt(salesOran, pub);
        var c3 = _engine.Encrypt(salesConstantine, pub);

        // Untrusted cloud server aggregates ciphertexts without seeing plaintexts
        var cSum12 = _engine.HomomorphicAdd(c1, c2, pub);
        var cTotal = _engine.HomomorphicAdd(cSum12, c3, pub);

        // Client decrypts total
        var decryptedTotal = _engine.Decrypt(cTotal, priv);

        // Assert
        decryptedTotal.Should().Be(salesAlgiers + salesOran + salesConstantine);
        decryptedTotal.Should().Be(325000);
    }

    [Fact]
    public void HomomorphicScalarMultiplication_MultipliesCiphertextByScalar_DecryptsToExactProduct()
    {
        // Arrange
        var (pub, priv) = _engine.GenerateKeyPair(bitLength: 128);
        long unitPrice = 750;
        long quantityMultiplier = 12;

        // Act
        var cPrice = _engine.Encrypt(unitPrice, pub);
        var cTotalCost = _engine.HomomorphicMultiply(cPrice, quantityMultiplier, pub);

        var decryptedCost = _engine.Decrypt(cTotalCost, priv);

        // Assert
        decryptedCost.Should().Be(unitPrice * quantityMultiplier);
        decryptedCost.Should().Be(9000);
    }

    [Fact]
    public void AddHomomorphicAnalyticsServices_RegistersDependenciesCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddHomomorphicAnalyticsServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(PaillierHomomorphicAnalyticsEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
