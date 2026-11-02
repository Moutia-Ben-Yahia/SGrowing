namespace SGrowing.Api.Modules.Finance.MicroPaymentMesh;

public record PaymentChannelEdge(
    string ChannelId,
    string NodeA,
    string NodeB,
    decimal CapacityTokens,
    decimal BalanceA,
    decimal BalanceB,
    decimal FeeBaseTokens,
    decimal FeeProportionalPpm,
    bool IsOpen);

public record MicroPaymentHop(
    string ChannelId,
    string FromNode,
    string ToNode,
    decimal AmountTokens,
    decimal FeeTokens);

public record RoutePaymentRequest(
    string SourceNode,
    string DestinationNode,
    decimal PaymentAmountTokens,
    string PaymentHash,
    List<PaymentChannelEdge> ActiveChannels,
    decimal MaxAllowedFeeTokens = 0.50m);

public record PaymentRouteResult(
    string PaymentHash,
    string SourceNode,
    string DestinationNode,
    decimal AmountTokens,
    decimal TotalFeeTokens,
    List<MicroPaymentHop> Hops,
    bool IsSuccessful,
    string StatusMessage);
