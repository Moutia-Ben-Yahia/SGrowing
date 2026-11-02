namespace SGrowing.Tests.Security;

using FluentAssertions;
using SGrowing.Api.Modules.Security.QkdSatelliteArbiter;
using Xunit;

public class QkdSatelliteArbiterTests
{
    private readonly QkdSatelliteArbiterEngine _engine = new();

    [Fact]
    public void DistillSecretKeys_LowQber_DistillsValidSecretKey()
    {
        // 100 pulses with matching bases and only 2 errors (2% QBER << 11%)
        var qubits = new List<QkdQubitTransmission>();
        for (int i = 0; i < 100; i++)
        {
            string basis = (i % 2 == 0) ? "Rectilinear" : "Diagonal";
            int bit = (i % 3 == 0) ? 1 : 0;
            int measuredBit = (i < 2) ? (1 - bit) : bit; // 2 errors out of 100
            qubits.Add(new QkdQubitTransmission(i, basis, bit, basis, measuredBit));
        }

        var request = new QkdSessionRequest("SAT-MICIOUS-LEO", "OGS-CANARY-01", qubits);
        var result = _engine.DistillSecretKeys(request);

        result.SessionAborted.Should().BeFalse();
        result.InterceptionDetected.Should().BeFalse();
        result.MeasuredQberPercent.Should().Be(2.0);
        result.SiftedKeyLength.Should().Be(100);
        result.FinalDistilledKeyBits.Should().BeGreaterThan(50);
        result.KeyFingerprintSha256.Should().NotBe("ABORTED_EAVESDROPPING_DETECTED");
        result.SecurityStatus.Should().Contain("UNCONDITIONAL_SECURITY_VERIFIED");
    }

    [Fact]
    public void DistillSecretKeys_EavesdroppingQberAbove11Percent_AbortsSessionAndYieldsZeroKeys()
    {
        // 50 pulses with matching bases but 15 errors (30% QBER > 11% threshold)
        var qubits = new List<QkdQubitTransmission>();
        for (int i = 0; i < 50; i++)
        {
            int bit = (i % 2 == 0) ? 1 : 0;
            int measuredBit = (i < 15) ? (1 - bit) : bit; // 15 errors = 30% QBER
            qubits.Add(new QkdQubitTransmission(i, "Rectilinear", bit, "Rectilinear", measuredBit));
        }

        var request = new QkdSessionRequest("SAT-QEYSSAT", "OGS-BOULDER", qubits);
        var result = _engine.DistillSecretKeys(request);

        result.SessionAborted.Should().BeTrue();
        result.InterceptionDetected.Should().BeTrue();
        result.MeasuredQberPercent.Should().Be(30.0);
        result.FinalDistilledKeyBits.Should().Be(0);
        result.KeyFingerprintSha256.Should().Be("ABORTED_EAVESDROPPING_DETECTED");
        result.SecurityStatus.Should().Contain("SECURITY_VIOLATION");
    }

    [Fact]
    public void DistillSecretKeys_EmptyQubitStream_AbortsGracefully()
    {
        var request = new QkdSessionRequest("SAT-TEST", "OGS-TEST", []);
        var result = _engine.DistillSecretKeys(request);

        result.SessionAborted.Should().BeTrue();
        result.FinalDistilledKeyBits.Should().Be(0);
        result.KeyFingerprintSha256.Should().Be("EMPTY_SESSION");
    }

    [Fact]
    public void DistillSecretKeys_SiftingPhase_FiltersMismatchedBases()
    {
        // 40 pulses where only 20 have matching bases
        var qubits = new List<QkdQubitTransmission>();
        for (int i = 0; i < 40; i++)
        {
            string aliceBasis = (i % 2 == 0) ? "Rectilinear" : "Diagonal";
            string bobBasis = (i < 20) ? aliceBasis : ((aliceBasis == "Rectilinear") ? "Diagonal" : "Rectilinear");
            qubits.Add(new QkdQubitTransmission(i, aliceBasis, 1, bobBasis, 1));
        }

        var request = new QkdSessionRequest("SAT-TEST", "OGS-TEST", qubits);
        var result = _engine.DistillSecretKeys(request);

        result.RawQubitsCount.Should().Be(40);
        result.SiftedKeyLength.Should().Be(20);
        result.MeasuredQberPercent.Should().Be(0.0);
        result.SessionAborted.Should().BeFalse();
    }
}
