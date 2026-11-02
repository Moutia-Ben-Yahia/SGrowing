using System.Numerics;

namespace SGrowing.Api.Modules.Security.HomomorphicAnalytics;

public sealed record PaillierPublicKey(
    string ModulusHex,
    string GeneratorHex
)
{
    public BigInteger N => BigInteger.Parse(ModulusHex, System.Globalization.NumberStyles.HexNumber);
    public BigInteger N2 => N * N;
    public BigInteger G => BigInteger.Parse(GeneratorHex, System.Globalization.NumberStyles.HexNumber);
}

public sealed record PaillierPrivateKey(
    string LambdaHex,
    string MuHex,
    PaillierPublicKey PublicKey
)
{
    public BigInteger Lambda => BigInteger.Parse(LambdaHex, System.Globalization.NumberStyles.HexNumber);
    public BigInteger Mu => BigInteger.Parse(MuHex, System.Globalization.NumberStyles.HexNumber);
}

public sealed record EncryptedMetricRecord(
    string MetricName,
    string CiphertextHex,
    DateTime CreatedAtUtc
);

public sealed record HomomorphicSumAggregationRequest(
    PaillierPublicKey PublicKey,
    IReadOnlyList<string> CiphertextsHex
);

public sealed record HomomorphicSumAggregationResponse(
    string AggregateCiphertextHex,
    int ItemsAggregated,
    DateTime ExecutedAtUtc
);

public sealed record DecryptQueryRequest(
    PaillierPrivateKey PrivateKey,
    string AggregateCiphertextHex,
    int TotalItems
);

public sealed record DecryptQueryResponse(
    long DecryptedSum,
    double DecryptedAverage,
    int RecordCount,
    DateTime DecryptedAtUtc
);
