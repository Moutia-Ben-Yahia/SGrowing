using FluentAssertions;
using MarketingStudio.Core.Common;
using Xunit;

namespace MarketingStudio.Tests.Common;

public class ResultTests
{
    [Fact]
    public void Success_ShouldCreateSuccessfulResult()
    {
        var result = Result<string>.Success("test payload");

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("test payload");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateFailureResult()
    {
        var result = Result<string>.Failure("Resource not found");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Resource not found");
        var act = () => { var v = result.Value; };
        act.Should().Throw<InvalidOperationException>();
    }
}
