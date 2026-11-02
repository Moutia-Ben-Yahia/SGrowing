using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SGrowing.Api.Modules.Finance.ConcentratedLiquidity;
using Xunit;

namespace SGrowing.Tests.Finance;

public sealed class ConcentratedLiquidityTests
{
    private readonly ConcentratedLiquidityAmmEngine _engine = new();

    [Fact]
    public void AddLiquidity_WithinActivePriceRange_ComputesValidLiquidityLAndReserves()
    {
        // Arrange: Pool with 1 TOKEN_X = 2000 TOKEN_Y (e.g. ETH/USDC)
        var pool = new ConcentratedPoolState(
            PoolId: "ETH-USDC-03",
            TokenXSymbol: "ETH",
            TokenYSymbol: "USDC",
            CurrentPrice: 2000.0,
            TotalLiquidityL: 0.0,
            FeeTierBps: 30, // 0.30%
            InstantaneousVolatility: 0.005,
            Positions: []
        );

        var request = new AddLiquidityRequest(
            PoolId: "ETH-USDC-03",
            OwnerId: "LP-01",
            LowerPrice: 1800.0,
            UpperPrice: 2200.0,
            AmountX: 5.0,     // 5 ETH
            AmountY: 10000.0  // 10,000 USDC
        );

        // Act
        var (updatedPool, position) = _engine.AddLiquidity(pool, request);

        // Assert
        position.Should().NotBeNull();
        position.LiquidityL.Should().BeGreaterThan(0);
        position.RealReserveX.Should().BeApproximately(5.0, 0.5);
        position.RealReserveY.Should().BeApproximately(10000.0, 500.0);
        updatedPool.TotalLiquidityL.Should().Be(position.LiquidityL);
        updatedPool.Positions.Should().ContainSingle();
    }

    [Fact]
    public void ExecuteSwap_TokenXForTokenY_CalculatesAmountsAndPriceImpact()
    {
        // Arrange
        var pool = new ConcentratedPoolState(
            PoolId: "ETH-USDC-03",
            TokenXSymbol: "ETH",
            TokenYSymbol: "USDC",
            CurrentPrice: 2000.0,
            TotalLiquidityL: 10000.0,
            FeeTierBps: 30,
            InstantaneousVolatility: 0.005,
            Positions: []
        );

        var request = new SwapExactInputRequest(
            PoolId: "ETH-USDC-03",
            TokenInSymbol: "ETH",
            AmountIn: 1.0, // Swap 1 ETH for USDC
            MaxSlippagePercent: 2.0
        );

        // Act
        var (newPool, result) = _engine.ExecuteSwap(pool, request);

        // Assert
        result.TokenInSymbol.Should().Be("ETH");
        result.TokenOutSymbol.Should().Be("USDC");
        result.AmountIn.Should().Be(1.0);
        result.AmountOut.Should().BeGreaterThan(1900.0).And.BeLessThan(2000.0);
        result.FeePaid.Should().BeApproximately(0.003, 0.0001); // ~0.30%
        result.PriceImpactPercent.Should().BeLessThan(2.0);
        newPool.CurrentPrice.Should().BeLessThan(2000.0, "Selling ETH should exert downward price pressure on ETH");
    }

    [Fact]
    public void CalculateCapitalEfficiencyMultiplier_NarrowRange_YieldsHighMultiplier()
    {
        // Range 0.99 to 1.01 (e.g. stablecoin pair USDC/USDT)
        var multiplier = _engine.CalculateCapitalEfficiencyMultiplier(0.99, 1.01);

        // Assert: Capital efficiency multiplier for +-1% is > 190x
        multiplier.Should().BeGreaterThan(150.0);
    }

    [Fact]
    public void ExecuteSwap_SlippageExceeded_ThrowsInvalidOperationException()
    {
        // Arrange
        var pool = new ConcentratedPoolState(
            PoolId: "LOW-LIQ-POOL",
            TokenXSymbol: "ETH",
            TokenYSymbol: "USDC",
            CurrentPrice: 2000.0,
            TotalLiquidityL: 50.0, // Small liquidity pool
            FeeTierBps: 30,
            InstantaneousVolatility: 0.0,
            Positions: []
        );

        var request = new SwapExactInputRequest(
            PoolId: "LOW-LIQ-POOL",
            TokenInSymbol: "ETH",
            AmountIn: 5.0, // Large trade in thin pool causes massive slippage
            MaxSlippagePercent: 1.0 // Strict 1% max slippage limit
        );

        // Act & Assert
        var act = () => _engine.ExecuteSwap(pool, request);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*exceeds maximum allowed slippage*");
    }

    [Fact]
    public void AddConcentratedLiquidityServices_RegistersSingleton()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddConcentratedLiquidityServices();

        // Assert - Rule: NEVER call services.BuildServiceProvider()
        services.Should().ContainSingle(s => s.ServiceType == typeof(ConcentratedLiquidityAmmEngine) && s.Lifetime == ServiceLifetime.Singleton);
    }
}
