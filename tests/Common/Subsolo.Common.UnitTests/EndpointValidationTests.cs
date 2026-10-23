using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0030 (H28-06, herança da revisão da SPEC-0028) — o helper de validação no endpoint mora no Common.Presentation
/// (<see cref="EndpointValidation"/>), com o comportamento do <c>EndpointValidation</c> do Events: pedido válido →
/// <see langword="null"/> (o endpoint segue para o handler); inválido → ProblemDetails 400 com o código da primeira falha
/// (não o código genérico do decorator) e as mensagens de cada campo inválido em <c>errors</c>, sem repetir mensagens.
/// </summary>
public sealed class EndpointValidationTests
{
    [Fact]
    [Trait("Spec", "SPEC-0030:H28-06")]
    public async Task AValidRequestReturnsNull()
    {
        var result = await EndpointValidation.ValidateAsync(
            new SampleValidator(), new SampleRequest("fcm", 3), "Pedido inválido.", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:H28-06")]
    public async Task AnInvalidRequestIs400WithTheCodeOfTheFirstFailureAndTheFields()
    {
        var problem = await EndpointValidation.ValidateAsync(
            new SampleValidator(), new SampleRequest(string.Empty, 0), "Pedido inválido.", TestContext.Current.CancellationToken);

        var (status, contentType, body) = await ExecuteAsync(problem.ShouldNotBeNull());
        status.ShouldBe(StatusCodes.Status400BadRequest);
        contentType.ShouldNotBeNull().ShouldStartWith("application/problem+json");
        body.GetProperty("code").GetString().ShouldBe("sample.invalid_name");
        body.GetProperty("detail").GetString().ShouldBe("Pedido inválido.");
        var errors = body.GetProperty("errors");
        Messages(errors, "Name").ShouldBe(["Informe o nome."]);
        Messages(errors, "Count").ShouldBe(["Informe um número positivo."]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0030:H28-06")]
    public void ToErrorGroupsTheMessagesByFieldWithoutRepeating()
    {
        var validation = new ValidationResult(
        [
            new ValidationFailure("Name", "Informe o nome.") { ErrorCode = "sample.invalid_name" },
            new ValidationFailure("Name", "Informe o nome.") { ErrorCode = "sample.invalid_name" },
            new ValidationFailure("Name", "Nome longo demais.") { ErrorCode = "sample.invalid_name" },
            new ValidationFailure("Count", "Informe um número positivo.") { ErrorCode = "sample.invalid_count" },
        ]);

        var error = EndpointValidation.ToError(validation, "Pedido inválido.");

        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe("sample.invalid_name");
        error.Description.ShouldBe("Pedido inválido.");
        var fields = error.ValidationErrors;
        fields["Name"].ShouldBe(["Informe o nome.", "Nome longo demais."]);
        fields["Count"].ShouldBe(["Informe um número positivo."]);
    }

    private static List<string?> Messages(JsonElement errors, string field) =>
        [.. errors.EnumerateObject()
            .Where(property => string.Equals(property.Name, field, StringComparison.OrdinalIgnoreCase))
            .ShouldHaveSingleItem($"errors sem o campo {field}: {errors}")
            .Value.EnumerateArray()
            .Select(message => message.GetString())];

    private static async Task<(int Status, string? ContentType, JsonElement Body)> ExecuteAsync(IResult result)
    {
        await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        body.Position = 0;
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: TestContext.Current.CancellationToken);
        return (context.Response.StatusCode, context.Response.ContentType, document.RootElement.Clone());
    }

    private sealed record SampleRequest(string Name, int Count);

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator()
        {
            RuleFor(request => request.Name).NotEmpty().WithErrorCode("sample.invalid_name").WithMessage("Informe o nome.");
            RuleFor(request => request.Count).GreaterThan(0).WithErrorCode("sample.invalid_count").WithMessage("Informe um número positivo.");
        }
    }
}
