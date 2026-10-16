using Microsoft.Extensions.Logging.Abstractions;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Domain;
using Subsolo.Common.UnitTests.Doubles;

namespace Subsolo.Common.UnitTests;

/// <summary>SPEC-0007 UT-01, UT-02 — decorator de validação (FluentValidation) em volta do handler.</summary>
public sealed class ValidationDecoratorTests
{
    private static readonly RegisterFan ValidCommand = new("Ana", "ana@subsolo.test");
    private static readonly RegisterFan InvalidCommand = new(string.Empty, "nao-e-um-email");

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-01")]
    public async Task DecoratedHandlerReturnsSuccessForValidCommand()
    {
        var inner = new RecordingHandler(Result.Success);
        var decorated = new LoggingDecorator.CommandHandler<RegisterFan>(
            new ValidationDecorator.CommandHandler<RegisterFan>(inner, [new RegisterFanValidator()]),
            NullLogger<LoggingDecorator.CommandHandler<RegisterFan>>.Instance);

        var result = await decorated.Handle(ValidCommand, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-01")]
    public async Task DecoratedHandlerWithResponseReturnsValueForValidCommand()
    {
        var id = Guid.NewGuid();
        var inner = new RecordingHandlerWithId(id);
        var decorated = new ValidationDecorator.CommandHandler<RegisterFanWithId, Guid>(
            inner, [new RegisterFanWithIdValidator()]);

        var result = await decorated.Handle(new RegisterFanWithId("Ana", "ana@subsolo.test"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(id);
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-02")]
    public async Task InvalidCommandBecomesValidationErrorListingBothFieldsWithoutCallingHandler()
    {
        var inner = new RecordingHandler(Result.Success);
        var decorated = new ValidationDecorator.CommandHandler<RegisterFan>(inner, [new RegisterFanValidator()]);

        var result = await decorated.Handle(InvalidCommand, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors.Keys.ShouldBe([nameof(RegisterFan.Name), nameof(RegisterFan.Email)], ignoreOrder: true);
        result.Error.ValidationErrors.Values.ShouldAllBe(messages => messages.Length > 0);
        inner.Calls.ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-02")]
    public async Task InvalidCommandWithResponseBecomesValidationErrorWithoutCallingHandler()
    {
        var inner = new RecordingHandlerWithId(Guid.NewGuid());
        var decorated = new ValidationDecorator.CommandHandler<RegisterFanWithId, Guid>(
            inner, [new RegisterFanWithIdValidator()]);

        var result = await decorated.Handle(new RegisterFanWithId(string.Empty, "nao-e-um-email"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors.Keys.ShouldBe([nameof(RegisterFanWithId.Name), nameof(RegisterFanWithId.Email)], ignoreOrder: true);
        inner.Calls.ShouldBe(0);
    }
}
