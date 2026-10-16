using Subsolo.Common.Domain;

namespace Subsolo.Common.UnitTests;

/// <summary>
/// SPEC-0007 UT-03 — as fábricas de <see cref="Error"/> produzem o tipo que define o status HTTP, guardam o código e
/// a descrição, e só o erro de validação carrega a lista de campos.
/// </summary>
public sealed class ErrorTests
{
    private const string Code = "Shows.Example";
    private const string Description = "Descrição do erro.";

    public static TheoryData<string, ErrorType> Factories => new()
    {
        { nameof(Error.Failure), ErrorType.Failure },
        { nameof(Error.NotFound), ErrorType.NotFound },
        { nameof(Error.Conflict), ErrorType.Conflict },
        { nameof(Error.Forbidden), ErrorType.Forbidden },
        { nameof(Error.Unauthorized), ErrorType.Unauthorized },
    };

    [Theory]
    [Trait("Spec", "SPEC-0007:UT-03")]
    [MemberData(nameof(Factories))]
    public void FactoryCreatesErrorOfItsTypeWithoutValidationErrors(string factory, ErrorType expectedType)
    {
        var error = Create(factory);

        error.Type.ShouldBe(expectedType);
        error.Code.ShouldBe(Code);
        error.Description.ShouldBe(Description);
        error.ValidationErrors.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-03")]
    public void ValidationFactoryCarriesTheFieldsAndMessages()
    {
        var fields = new Dictionary<string, string[]>
        {
            ["Name"] = ["Nome obrigatório."],
            ["Email"] = ["E-mail inválido.", "E-mail muito longo."],
        };

        var error = Error.Validation(Code, Description, fields);

        error.Type.ShouldBe(ErrorType.Validation);
        error.Code.ShouldBe(Code);
        error.Description.ShouldBe(Description);
        error.ValidationErrors.Keys.ShouldBe(["Name", "Email"], ignoreOrder: true);
        error.ValidationErrors["Email"].ShouldBe(["E-mail inválido.", "E-mail muito longo."]);
    }

    [Fact]
    [Trait("Spec", "SPEC-0007:UT-03")]
    public void NoneIsTheErrorOfASuccessfulResult()
    {
        Result.Success().Error.ShouldBe(Error.None);
        Error.None.Code.ShouldBeEmpty();
        Should.Throw<ArgumentException>(() => Result.Failure(Error.None));
    }

    private static Error Create(string factory) => factory switch
    {
        nameof(Error.Failure) => Error.Failure(Code, Description),
        nameof(Error.NotFound) => Error.NotFound(Code, Description),
        nameof(Error.Conflict) => Error.Conflict(Code, Description),
        nameof(Error.Forbidden) => Error.Forbidden(Code, Description),
        nameof(Error.Unauthorized) => Error.Unauthorized(Code, Description),
        _ => throw new ArgumentOutOfRangeException(nameof(factory), factory, "Fábrica desconhecida."),
    };
}
