using FluentAssertions;
using SGrowing.Core.Common;
using Xunit;

namespace SGrowing.Tests.Common;

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
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Resource not found*");
    }

    [Fact]
    public void NonGenericResult_Success_ShouldHaveIsSuccessTrueAndNullError()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void NonGenericResult_Failure_ShouldHaveIsSuccessFalseAndError()
    {
        var result = Result.Failure("Database timeout");

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Database timeout");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Result_WithFailureAndNullOrWhitespaceError_ThrowsInvalidOperationException(string? invalidError)
    {
        var actNonGeneric = () => Result.Failure(invalidError!);
        actNonGeneric.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed result must have an error message.");

        var actGeneric = () => Result<int>.Failure(invalidError!);
        actGeneric.Should().Throw<InvalidOperationException>()
            .WithMessage("Failed result must have an error message.");
    }

    [Fact]
    public void GenericResult_WithValueTypes_ShouldWorkCorrectly()
    {
        var intResult = Result<int>.Success(42);
        intResult.IsSuccess.Should().BeTrue();
        intResult.Value.Should().Be(42);

        var guid = Guid.NewGuid();
        var guidResult = Result<Guid>.Success(guid);
        guidResult.IsSuccess.Should().BeTrue();
        guidResult.Value.Should().Be(guid);

        var boolResult = Result<bool>.Success(true);
        boolResult.IsSuccess.Should().BeTrue();
        boolResult.Value.Should().BeTrue();
    }

    [Fact]
    public void GenericResult_AccessingValueOnFailure_ThrowsInvalidOperationExceptionWithErrorMessage()
    {
        var result = Result<int>.Failure("Invalid operation occurred");

        var act = () => { var val = result.Value; };
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot access Value of a failure Result: Invalid operation occurred");
    }
}
