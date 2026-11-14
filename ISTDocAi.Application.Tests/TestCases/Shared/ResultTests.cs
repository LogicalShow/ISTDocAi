using ISTDocAi.Domain.Common.Shared.Response;

namespace ISTDocAi.Application.Tests.TestCases.Shared;

[TestFixture]
public class ResultTests
{
    [Test]
    public void Value_ShouldReturnValue_WhenResultIsSuccessful()
    {
        var result = Result.Success(42);

        Assert.That(result.Value, Is.EqualTo(42));
    }

    [Test]
    public void Value_ShouldThrow_WhenResultIsFailure()
    {
        var result = Result.Failure<int>(
            Error.NotFound("Item.NotFound", "Item not found."));

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            _ = result.Value;
        });

        Assert.That(
            exception!.Message,
            Is.EqualTo("Cannot access the value of a failed result. Error: Item.NotFound"));
    }

    [Test]
    public void Constructor_ShouldRejectSuccessfulResultWithError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TestResult(
                isSuccess: true,
                Error.Failure("Test.Error", "Test error.")));

        Assert.That(
            exception!.Message,
            Is.EqualTo("A successful result cannot contain an error."));
    }

    [Test]
    public void Constructor_ShouldRejectFailedResultWithoutError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new TestResult(isSuccess: false, Error.None));

        Assert.That(
            exception!.Message,
            Is.EqualTo("A failed result must contain an error."));
    }

    private sealed class TestResult(bool isSuccess, Error error)
        : Result(isSuccess, error);
}
