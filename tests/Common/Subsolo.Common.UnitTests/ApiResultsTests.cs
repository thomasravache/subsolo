using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Subsolo.Common.Domain;
using Subsolo.Common.Presentation;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0007 UT-03 — o mapeador Result → HTTP produz o status da tabela do contrato e um ProblemDetails
/// (RFC 9457) com o código do erro em <c>extensions.code</c> (no JSON, a propriedade <c>code</c> no topo).
/// </summary>
public sealed class ApiResultsTests
{
    private const string Code = "Shows.Example";

    [Theory]
    [Trait("Spec", "SPEC-0007:UT-03")]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public async Task EachErrorTypeMapsToItsStatusWithCodeInExtensions(ErrorType type, int expectedStatus)
    {
        var result = Result.Failure(new Error(Code, "Descrição do erro.", type));

        var (status, contentType, body) = await ExecuteAsync(ApiResults.Problem(result));

        status.ShouldBe(expectedStatus);
        contentType.ShouldNotBeNull().ShouldStartWith("application/problem+json");
        body.GetProperty("status").GetInt32().ShouldBe(expectedStatus);
        body.GetProperty("code").GetString().ShouldBe(Code);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-03")]
    public async Task ValidationErrorListsTheFieldsInErrors()
    {
        var fields = new Dictionary<string, string[]>
        {
            ["Name"] = ["Nome obrigatório."],
            ["Email"] = ["E-mail inválido.", "E-mail muito longo."],
        };
        var result = Result.Failure(Error.Validation(Code, "Um ou mais campos são inválidos.", fields));

        var (status, _, body) = await ExecuteAsync(ApiResults.Problem(result));

        status.ShouldBe(StatusCodes.Status400BadRequest);
        body.GetProperty("code").GetString().ShouldBe(Code);
        var errors = body.GetProperty("errors");
        errors.EnumerateObject().Select(field => field.Name).ShouldBe(["Name", "Email"], ignoreOrder: true);
        errors.GetProperty("Name").EnumerateArray().Select(message => message.GetString()).ShouldBe(["Nome obrigatório."]);
        errors.GetProperty("Email").EnumerateArray().Select(message => message.GetString())
            .ShouldBe(["E-mail inválido.", "E-mail muito longo."]);
    }

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
}
