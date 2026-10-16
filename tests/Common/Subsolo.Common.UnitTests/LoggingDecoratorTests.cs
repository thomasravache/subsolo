using Microsoft.Extensions.Logging.Testing;
using Subsolo.Common.Application.Decorators;
using Subsolo.Common.Domain;
using Subsolo.Common.UnitTests.Doubles;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0007 UT-04 — o decorator de log (comando, comando com resposta e consulta) registra o nome e o resultado,
/// nunca o payload (o e-mail é dado pessoal, ADR-0008). Verifica a mensagem, o estado estruturado e os escopos de
/// cada registro.
/// </summary>
public sealed class LoggingDecoratorTests
{
    private const string Email = "fa.privada@subsolo.test";
    private static readonly RegisterFan Command = new("Ana", Email);

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task SuccessLogsCommandNameWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.CommandHandler<RegisterFan>>();
        var decorated = new LoggingDecorator.CommandHandler<RegisterFan>(new RecordingHandler(Result.Success), logger);

        var result = await decorated.Handle(Command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(RegisterFan), Case.Sensitive, "o log não registra o nome do comando");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task FailureLogsCommandNameAndErrorCodeWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.CommandHandler<RegisterFan>>();
        var error = new Error("Fans.EmailTaken", "E-mail já cadastrado.", ErrorType.Conflict);
        var decorated = new LoggingDecorator.CommandHandler<RegisterFan>(
            new RecordingHandler(() => Result.Failure(error)), logger);

        var result = await decorated.Handle(Command, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(RegisterFan), Case.Sensitive, "o log não registra o nome do comando");
        logged.ShouldContain(error.Code, Case.Sensitive, "o log não registra o resultado (código do erro)");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task CommandWithResponseLogsNameAndOutcomeWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.CommandHandler<RegisterFanWithId, Guid>>();
        var inner = new ConfigurableHandlerWithId(() => Result.Success(Guid.NewGuid()));
        var decorated = new LoggingDecorator.CommandHandler<RegisterFanWithId, Guid>(inner, logger);

        var result = await decorated.Handle(new RegisterFanWithId("Ana", Email), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        inner.Calls.ShouldBe(1);
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(RegisterFanWithId), Case.Sensitive, "o log não registra o nome do comando");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task FailedCommandWithResponseLogsErrorCodeWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.CommandHandler<RegisterFanWithId, Guid>>();
        var error = Error.Conflict("Fans.EmailTaken", "E-mail já cadastrado.");
        var decorated = new LoggingDecorator.CommandHandler<RegisterFanWithId, Guid>(
            new ConfigurableHandlerWithId(() => Result.Failure<Guid>(error)), logger);

        var result = await decorated.Handle(new RegisterFanWithId("Ana", Email), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(error);
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(RegisterFanWithId), Case.Sensitive, "o log não registra o nome do comando");
        logged.ShouldContain(error.Code, Case.Sensitive, "o log não registra o resultado (código do erro)");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task QueryLogsNameAndOutcomeWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.QueryHandler<FindFanByEmail, string>>();
        var inner = new RecordingQueryHandler(() => Result.Success("Ana"));
        var decorated = new LoggingDecorator.QueryHandler<FindFanByEmail, string>(inner, logger);

        var result = await decorated.Handle(new FindFanByEmail(Email), TestContext.Current.CancellationToken);

        result.Value.ShouldBe("Ana");
        inner.Calls.ShouldBe(1);
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(FindFanByEmail), Case.Sensitive, "o log não registra o nome da consulta");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-04")]
    public async Task FailedQueryLogsErrorCodeWithoutPayload()
    {
        var logger = new FakeLogger<LoggingDecorator.QueryHandler<FindFanByEmail, string>>();
        var error = Error.NotFound("Fans.NotFound", "Fã não encontrado.");
        var decorated = new LoggingDecorator.QueryHandler<FindFanByEmail, string>(
            new RecordingQueryHandler(() => Result.Failure<string>(error)), logger);

        var result = await decorated.Handle(new FindFanByEmail(Email), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(error);
        var logged = LoggedText(logger);
        logged.ShouldContain(nameof(FindFanByEmail), Case.Sensitive, "o log não registra o nome da consulta");
        logged.ShouldContain(error.Code, Case.Sensitive, "o log não registra o resultado (código do erro)");
        logged.ShouldNotContain(Email, Case.Insensitive, "o log expôs o e-mail do payload");
    }

    /// <summary>Tudo o que os registros carregam: mensagem, estado estruturado, escopos e exceção.</summary>
    private static string LoggedText<T>(FakeLogger<T> logger)
    {
        var records = logger.Collector.GetSnapshot();
        records.ShouldNotBeEmpty("o decorator não registrou nada");

        var parts = new List<string?>();
        foreach (var record in records)
        {
            parts.Add(record.Message);
            parts.Add(record.Exception?.ToString());
            parts.AddRange(record.StructuredState?.Select(pair => $"{pair.Key}={pair.Value}") ?? []);
            foreach (var scope in record.Scopes)
            {
                parts.Add(scope?.ToString());
                if (scope is IEnumerable<KeyValuePair<string, object?>> values)
                {
                    parts.AddRange(values.Select(pair => $"{pair.Key}={pair.Value}"));
                }
            }
        }

        return string.Join('\n', parts.Where(part => part is not null));
    }
}
