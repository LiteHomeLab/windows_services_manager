using System;
using FluentAssertions;
using WinServiceManager.Models;
using Xunit;

namespace WinServiceManager.Tests.UnitTests;

/// <summary>
/// ServiceOperationResult 单元测试
/// </summary>
public class ServiceOperationResultTests
{
    [Fact]
    public void SuccessResult_ShouldCreateSuccessResult()
    {
        // Arrange
        var message = "Operation completed successfully";

        // Act
        var result = ServiceOperationResult.SuccessResult(message);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be(message);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SuccessResult_WithNullMessage_ShouldCreateSuccessResult()
    {
        // Act
        var result = ServiceOperationResult.SuccessResult(null);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void SuccessResult_WithEmptyMessage_ShouldCreateSuccessResult()
    {
        // Act
        var result = ServiceOperationResult.SuccessResult("");

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailureResult_WithMessage_ShouldCreateFailureResult()
    {
        // Arrange
        var message = "Operation failed";

        // Act
        var result = ServiceOperationResult.FailureResult(message);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(message);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void FailureResult_WithException_ShouldCreateFailureResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = ServiceOperationResult.FailureResult(exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Test exception");
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void FailureResult_WithMessageAndException_ShouldCreateFailureResult()
    {
        // Arrange
        var message = "Custom error message";
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = ServiceOperationResult.FailureResult(message, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be(message);
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void FailureResult_WithNullMessageAndException_ShouldCreateFailureResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = ServiceOperationResult.FailureResult(null, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Test exception");
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void ServiceOperationResult_Generic_SuccessResult_ShouldCreateSuccessResult()
    {
        // Arrange
        var data = "Test Data";
        var message = "Operation completed";

        // Act
        var result = ServiceOperationResult<string>.SuccessResult(data, message);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(data);
        result.Message.Should().Be(message);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ServiceOperationResult_Generic_SuccessResult_WithoutMessage_ShouldCreateSuccessResult()
    {
        // Arrange
        var data = 42;

        // Act
        var result = ServiceOperationResult<int>.SuccessResult(data);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().Be(42);
        result.Message.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ServiceOperationResult_Generic_FailureResult_WithMessage_ShouldCreateFailureResult()
    {
        // Arrange
        var message = "Operation failed";

        // Act
        var result = ServiceOperationResult<object>.FailureResult(message);

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Be(message);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void ServiceOperationResult_Generic_FailureResult_WithException_ShouldCreateFailureResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = ServiceOperationResult<object>.FailureResult(exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Contain("Test exception");
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void ServiceOperationResult_Generic_FailureResult_WithMessageAndException_ShouldCreateFailureResult()
    {
        // Arrange
        var message = "Custom error message";
        var exception = new InvalidOperationException("Test exception");

        // Act
        var result = ServiceOperationResult<object>.FailureResult(message, exception);

        // Assert
        result.Success.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Message.Should().Be(message);
        result.Error.Should().Be(exception);
    }

    [Fact]
    public void ServiceOperationResult_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var successResult = ServiceOperationResult.SuccessResult("Success");
        var failureResult = ServiceOperationResult.FailureResult("Failure");

        // Act
        var successString = successResult.ToString();
        var failureString = failureResult.ToString();

        // Assert
        successString.Should().Be("Success: Success");
        failureString.Should().Be("Failure: Failure");
    }

    [Fact]
    public void ServiceOperationResult_ToString_WithNullMessage_ShouldReturnDefaultMessage()
    {
        // Arrange
        var result = ServiceOperationResult.SuccessResult(null);

        // Act
        var resultString = result.ToString();

        // Assert
        resultString.Should().Be("Success");
    }

    [Fact]
    public void ServiceOperationResult_Generic_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var successResult = ServiceOperationResult<int>.SuccessResult(42, "Success");
        var failureResult = ServiceOperationResult<string>.FailureResult("Failure");

        // Act
        var successString = successResult.ToString();
        var failureString = failureResult.ToString();

        // Assert
        successString.Should().Be("Success: Success (Data: 42)");
        failureString.Should().Be("Failure: Failure");
    }

    [Fact]
    public void ServiceOperationResult_Generic_ToString_WithNullData_ShouldReturnFormattedString()
    {
        // Arrange
        var result = ServiceOperationResult<object>.SuccessResult(null, "Success");

        // Act
        var resultString = result.ToString();

        // Assert
        resultString.Should().Be("Success: Success");
    }

    [Fact]
    public void ServiceOperationResult_Generic_ToString_WithNullMessage_ShouldReturnDefaultMessage()
    {
        // Arrange
        var result = ServiceOperationResult<int>.SuccessResult(42);

        // Act
        var resultString = result.ToString();

        // Assert
        resultString.Should().Be("Success (Data: 42)");
    }

    [Fact]
    public void ServiceOperationResult_Generic_ToString_WithComplexData_ShouldReturnFormattedString()
    {
        // Arrange
        var data = new { Name = "Test", Value = 123 };
        var result = ServiceOperationResult<object>.SuccessResult(data, "Complex data");

        // Act
        var resultString = result.ToString();

        // Assert
        resultString.Should().Be("Success: Complex data (Data: { Name = Test, Value = 123 })");
    }

    [Fact]
    public void ServiceOperationResult_CanBeUsedInConditionalStatements()
    {
        // Arrange
        var success = ServiceOperationResult.SuccessResult();
        var failure = ServiceOperationResult.FailureResult("Error");

        // Act & Assert
        if (success)
        {
            Assert.True(true);
        }
        else
        {
            Assert.True(false, "Success result should be true in conditional");
        }

        if (!failure)
        {
            Assert.True(true);
        }
        else
        {
            Assert.True(false, "Failure result should be false in conditional");
        }
    }

    [Fact]
    public void ServiceOperationResult_Generic_CanBeUsedInConditionalStatements()
    {
        // Arrange
        var success = ServiceOperationResult<string>.SuccessResult("data");
        var failure = ServiceOperationResult<string>.FailureResult("Error");

        // Act & Assert
        if (success)
        {
            Assert.True(true);
        }
        else
        {
            Assert.True(false, "Success result should be true in conditional");
        }

        if (!failure)
        {
            Assert.True(true);
        }
        else
        {
            Assert.True(false, "Failure result should be false in conditional");
        }
    }

    [Fact]
    public void ServiceOperationResult_PatternMatching_ShouldWorkCorrectly()
    {
        // Arrange
        var results = new[]
        {
            ServiceOperationResult.SuccessResult("Success 1"),
            ServiceOperationResult.FailureResult("Failure 1"),
            ServiceOperationResult.SuccessResult("Success 2"),
            ServiceOperationResult.FailureResult("Failure 2")
        };

        // Act
        var successCount = 0;
        var failureCount = 0;

        foreach (var result in results)
        {
            switch (result.Success)
            {
                case true:
                    successCount++;
                    break;
                case false:
                    failureCount++;
                    break;
            }
        }

        // Assert
        successCount.Should().Be(2);
        failureCount.Should().Be(2);
    }

    [Fact]
    public void ServiceOperationResult_Generic_DataAccess_ShouldWorkCorrectly()
    {
        // Arrange
        var testData = new { Id = 1, Name = "Test" };
        var result = ServiceOperationResult<object>.SuccessResult(testData);

        // Act
        var dataAccess = result.Data != null ? "Data exists" : "Data is null";

        // Assert
        dataAccess.Should().Be("Data exists");
        result.Data.Should().BeSameAs(testData);
    }
}