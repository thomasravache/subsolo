using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Application.Messaging;
using Subsolo.Common.Domain;
using Subsolo.Common.UnitTests.Infrastructure;
using Subsolo.Modules.Template.Application;
using Subsolo.Modules.Template.Application.Features.CreateTemplateNote;
using Subsolo.Modules.Template.Application.Features.GetTemplateItemName;
using Subsolo.Modules.Template.Application.Features.RenameTemplateItem;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0007 UT-05 — num módulo registrado com <c>AddModule</c>, os handlers de comando, de comando com resposta e
/// de consulta são resolvidos já decorados, com o log por fora da validação (Logging → Validation → handler): um
/// comando inválido não chega ao handler, e o log registra o nome e o erro de validação devolvido por dentro.
/// </summary>
public sealed class ModuleRegistrationTests
{
    [Fact]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public async Task CommandHandlerIsResolvedWithLoggingAroundValidation()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<TemplateHandlerCalls>();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<RenameTemplateItemCommand>>();
        var ct = TestContext.Current.CancellationToken;

        handler.GetType().Assembly.ShouldBe(typeof(LoggingDecorator).Assembly, "o handler do módulo foi resolvido sem decorators");

        var invalid = await handler.Handle(new RenameTemplateItemCommand(Guid.Empty, string.Empty), ct);
        invalid.Error.Type.ShouldBe(ErrorType.Validation);
        invalid.Error.ValidationErrors.Keys.ShouldBe(
            [nameof(RenameTemplateItemCommand.ItemId), nameof(RenameTemplateItemCommand.Name)], ignoreOrder: true);
        calls.Count(nameof(RenameTemplateItemCommand)).ShouldBe(0, "o comando inválido chegou ao handler");
        LoggedOutcome(services, nameof(RenameTemplateItemCommand));

        var valid = await handler.Handle(new RenameTemplateItemCommand(Guid.NewGuid(), "Novo nome"), ct);
        valid.IsSuccess.ShouldBeTrue();
        calls.Count(nameof(RenameTemplateItemCommand)).ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public async Task CommandWithResponseHandlerIsResolvedWithLoggingAroundValidation()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<TemplateHandlerCalls>();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CreateTemplateNoteCommand, Guid>>();
        var ct = TestContext.Current.CancellationToken;

        handler.GetType().Assembly.ShouldBe(typeof(LoggingDecorator).Assembly, "o handler do módulo foi resolvido sem decorators");

        var invalid = await handler.Handle(new CreateTemplateNoteCommand(string.Empty), ct);
        invalid.Error.Type.ShouldBe(ErrorType.Validation);
        invalid.Error.ValidationErrors.Keys.ShouldBe([nameof(CreateTemplateNoteCommand.Text)]);
        calls.Count(nameof(CreateTemplateNoteCommand)).ShouldBe(0, "o comando inválido chegou ao handler");
        LoggedOutcome(services, nameof(CreateTemplateNoteCommand));

        var valid = await handler.Handle(new CreateTemplateNoteCommand("Uma nota"), ct);
        valid.Value.ShouldNotBe(Guid.Empty);
        calls.Count(nameof(CreateTemplateNoteCommand)).ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-05")]
    public async Task QueryHandlerIsResolvedWithLogging()
    {
        await using var services = TemplateModuleServices.Build();
        await using var scope = services.CreateAsyncScope();
        var calls = scope.ServiceProvider.GetRequiredService<TemplateHandlerCalls>();
        var handler = scope.ServiceProvider.GetRequiredService<IQueryHandler<GetTemplateItemNameQuery, string>>();

        handler.GetType().Assembly.ShouldBe(typeof(LoggingDecorator).Assembly, "o handler do módulo foi resolvido sem decorators");

        var result = await handler.Handle(new GetTemplateItemNameQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        calls.Count(nameof(GetTemplateItemNameQuery)).ShouldBe(1);
        TemplateModuleServices.LoggedText(services)
            .ShouldContain(nameof(GetTemplateItemNameQuery), Case.Sensitive, "a consulta não passou pelo decorator de log");
    }

    /// <summary>
    /// O decorator de log viu o erro de validação: ele está por fora do decorator de validação (se estivesse por
    /// dentro, ou ausente, o código do erro de validação não apareceria no log).
    /// </summary>
    private static void LoggedOutcome(IServiceProvider services, string messageName)
    {
        var logged = TemplateModuleServices.LoggedText(services);
        logged.ShouldContain(messageName, Case.Sensitive, "o comando não passou pelo decorator de log");
        logged.ShouldContain(ValidationDecorator.ErrorCode, Case.Sensitive, "o log não está por fora da validação");
    }
}
