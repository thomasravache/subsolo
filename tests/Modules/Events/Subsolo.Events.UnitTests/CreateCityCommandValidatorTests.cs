using FluentValidation.TestHelper;
using Subsolo.Modules.Events.Application.Features.CreateCity;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0017 UT-02 — nome com 1 ou 61 caracteres, UF <c>XX</c>, fuso <c>Mars/Olympus</c> e slug <c>Cidade_X</c> →
/// <c>events.invalid_city</c> no campo; cada endereço reservado do §2 → <c>events.city_slug_reserved</c> (o código vai
/// no <c>ErrorCode</c> da falha).
/// </summary>
public sealed class CreateCityCommandValidatorTests
{
    private const string InvalidCity = "events.invalid_city";
    private const string SlugReserved = "events.city_slug_reserved";

    /// <summary>Endereços reservados do §2 da spec.</summary>
    public static readonly TheoryData<string> ReservedSlugs =
    [
        "entrar", "conta", "admin", "apoie", "privacidade", "termos", "api", "health", "_next", "show", "banda", "casa",
        "produtor", "buscar", "seguindo", "offline",
    ];

    /// <summary>As 27 UFs.</summary>
    public static readonly TheoryData<string> States =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ",
        "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    ];

    private readonly CreateCityCommandValidator _validator = new();

    private static CreateCityCommand Valid(
        string name = "São José dos Pinhais",
        string state = "PR",
        string timeZone = "America/Sao_Paulo",
        string? slug = null) => new(name, state, timeZone, slug);

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [InlineData(1)]
    [InlineData(61)]
    public void NameOutside2To60CharactersFailsWithInvalidCity(int length)
    {
        var result = _validator.TestValidate(Valid(name: new string('a', length)));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidCity);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [InlineData("XX")]
    [InlineData("pr")]
    [InlineData("")]
    public void UnknownStateFailsWithInvalidCity(string state)
    {
        var result = _validator.TestValidate(Valid(state: state));

        result.ShouldHaveValidationErrorFor(command => command.State).WithErrorCode(InvalidCity);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [InlineData("Mars/Olympus")]
    [InlineData("")]
    public void InvalidTimeZoneFailsWithInvalidCity(string timeZone)
    {
        var result = _validator.TestValidate(Valid(timeZone: timeZone));

        result.ShouldHaveValidationErrorFor(command => command.TimeZone).WithErrorCode(InvalidCity);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [InlineData("Cidade_X")]
    [InlineData("a")]
    [InlineData("Cidade-X")]
    public void SlugOutsideThePatternFailsWithInvalidCity(string slug)
    {
        var result = _validator.TestValidate(Valid(slug: slug));

        result.ShouldHaveValidationErrorFor(command => command.Slug).WithErrorCode(InvalidCity);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [MemberData(nameof(ReservedSlugs))]
    public void ReservedSlugFailsWithCitySlugReserved(string slug)
    {
        var result = _validator.TestValidate(Valid(slug: slug));

        result.ShouldHaveValidationErrorFor(command => command.Slug).WithErrorCode(SlugReserved);
    }

    // Guarda: o validador ainda vazio já aceita os comandos válidos; continua passando depois das regras.
    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [MemberData(nameof(States))]
    public void EveryBrazilianStateIsAccepted(string state)
    {
        var result = _validator.TestValidate(Valid(state: state));

        result.ShouldNotHaveAnyValidationErrors();
    }

    // Guarda: idem.
    [Theory]
    [Trait("Spec", "SPEC-0017:UT-02")]
    [InlineData("Sp", "SP", "America/Sao_Paulo", null)]
    [InlineData("São José dos Pinhais", "PR", "America/Sao_Paulo", "sao-jose-dos-pinhais")]
    [InlineData("Manaus", "AM", "America/Manaus", "manaus-2")]
    [InlineData("Fernando de Noronha", "PE", "America/Noronha", null)]
    public void ValidCommandPasses(string name, string state, string timeZone, string? slug)
    {
        var result = _validator.TestValidate(new CreateCityCommand(name, state, timeZone, slug));

        result.ShouldNotHaveAnyValidationErrors();
    }

    // Guarda: idem (60 caracteres é o limite aceito).
    [Fact]
    [Trait("Spec", "SPEC-0017:UT-02")]
    public void NameWith60CharactersPasses()
    {
        var result = _validator.TestValidate(Valid(name: new string('a', 60)));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
