using KnowledgeSearch.Core.Common;
using Shouldly;
using Xunit;

namespace Api.Tests;

public class When_ResultIsCreated
{
    private static readonly string[] ExpectedRoots = ["Documentation"];

    [Fact]
    public void Then_SuccessResultHasNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void Then_ValidationResultHasValidationError()
    {
        var result = Result.Validation("Cada source requiere id, name y hostPath.");

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Kind.ShouldBe(ErrorKind.Validation);
        result.Error.Code.ShouldBe("validation");
        result.Error.Message.ShouldBe("Cada source requiere id, name y hostPath.");
    }

    [Fact]
    public void Then_GenericSuccessCarriesValue()
    {
        var result = Result.Success(ExpectedRoots);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(ExpectedRoots);
    }

    [Fact]
    public void Then_GenericResultCanBeCreatedFromValue()
    {
        Result<string> result = "Documentation";

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Documentation");
    }

    [Fact]
    public void Then_GenericResultCanBeCreatedFromError()
    {
        Result<string> result = new ResultError("not_found", "No existe.", ErrorKind.NotFound);

        result.IsSuccess.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
        result.Error.Kind.ShouldBe(ErrorKind.NotFound);
    }

    [Fact]
    public void Then_GenericResultCanBeResolved()
    {
        Result<string> success = "Documentation";
        Result<string> failure = new ResultError("not_found", "No existe.", ErrorKind.NotFound);

        success.Resolve(value => value.ToUpperInvariant(), error => error.Code)
            .ShouldBe("DOCUMENTATION");
        failure.Resolve(value => value.ToUpperInvariant(), error => error.Code)
            .ShouldBe("not_found");
    }
}
