using FluentValidation.Results;
using Subsolo.Events.UnitTests.Infrastructure;
using Subsolo.Modules.Events.Application.Features.CreateShow;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0023 UT-02 — o validador do cadastro de show (cidades, estilos e relógio de teste, "agora" = 18/10/2026): título
/// de 1 e 101 caracteres, data no passado, 0 e 16 bandas no line-up, 0 e 6 estilos, preço de 41 caracteres,
/// <c>ticketUrl</c> <c>http://x</c>, casa sem perfil nem nome, cidade e estilo inexistentes: cada erro vem ligado ao
/// campo, com <c>events.invalid_show</c> no <c>ErrorCode</c>. O show válido passa.
/// </summary>
public sealed class CreateShowCommandValidatorTests
{
    private const string InvalidShow = "events.invalid_show";

    private readonly ShowsHarness _harness = new();

    private static CreateShowCommand Valid() => ShowsHarness.NoiteDaPeste(TestProfiles.OwnerA, TestProfiles.ProducerA.Id);

    private Task<ValidationResult> ValidateAsync(CreateShowCommand command) =>
        _harness.Create<CreateShowCommandValidator>().ValidateAsync(command, TestContext.Current.CancellationToken);

    private static IReadOnlyList<ShowLineupItemInput> Bands(int count) =>
        [.. Enumerable.Range(1, count).Select(index => new ShowLineupItemInput(null, $"Banda {index}"))];

    /// <summary>O erro está no campo <paramref name="field"/> (ou num item dele, ex.: <c>Lineup[0].Name</c>) com o código do §6.</summary>
    private static void ShouldFailOn(ValidationResult result, string field)
    {
        var listing = string.Join("; ", result.Errors.Select(error => $"{error.PropertyName}={error.ErrorCode}"));
        result.Errors.ShouldContain(
            error => error.PropertyName.StartsWith(field, StringComparison.OrdinalIgnoreCase) && error.ErrorCode == InvalidShow,
            $"sem erro {InvalidShow} no campo {field} (erros: {(listing.Length == 0 ? "nenhum" : listing)})");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AValidShowPasses()
    {
        var result = await ValidateAsync(Valid());

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")));
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AFreeVenueNameAndAFreeTicketlessShowPass()
    {
        var result = await ValidateAsync(Valid() with
        {
            Venue = new ShowVenueInput(null, "Bar do Zé"),
            TicketUrl = null,
            Price = "Gratuito",
            Lineup = Bands(15),
            Styles = ["death", "black", "grind", "doom", "thrash"],
        });

        result.Errors.ShouldBeEmpty(string.Join("; ", result.Errors.Select(error => $"{error.PropertyName}: {error.ErrorMessage}")));
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData(1)]
    [InlineData(101)]
    public async Task ATitleOutsideTwoToOneHundredCharactersFails(int length)
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Title = new string('x', length) }), "Title");
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData("2026-10-17T22:00")]
    [InlineData("2026-10-18T08:00")]
    public async Task AStartInThePastInTheCityTimeZoneFails(string startsLocal)
    {
        // "Agora" é 09:00 de 18/10 em Curitiba: 08:00 do mesmo dia já passou.
        ShouldFailOn(await ValidateAsync(Valid() with { StartsLocal = startsLocal }), "StartsLocal");
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData("14/11/2026 22:00")]
    [InlineData("2026-11-14")]
    [InlineData("")]
    public async Task AStartOutsideTheLocalDateTimeFormatFails(string startsLocal)
    {
        ShouldFailOn(await ValidateAsync(Valid() with { StartsLocal = startsLocal }), "StartsLocal");
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData(0)]
    [InlineData(16)]
    public async Task ALineupOutsideOneToFifteenBandsFails(int count)
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Lineup = Bands(count) }), "Lineup");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task ALineupItemWithoutProfileNorNameFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Lineup = [new ShowLineupItemInput(null, null)] }), "Lineup");
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task StylesOutsideOneToFiveFail(int count)
    {
        string[] styles = ["death", "black", "grind", "doom", "thrash", "crust"];

        ShouldFailOn(await ValidateAsync(Valid() with { Styles = styles[..count] }), "Styles");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AnUnknownStyleFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Styles = ["death", "axé"] }), "Styles");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AnUnknownCityFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { City = "gotham" }), "City");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task APriceOfFortyOneCharactersFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Price = new string('9', 41) }), "Price");
    }

    [Theory]
    [Trait("Spec", "SPEC-0023:UT-02")]
    [InlineData("http://x")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ingressos")]
    public async Task ATicketUrlThatIsNotHttpsFails(string ticketUrl)
    {
        ShouldFailOn(await ValidateAsync(Valid() with { TicketUrl = ticketUrl }), "TicketUrl");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AVenueWithoutProfileNorNameFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Venue = new ShowVenueInput(null, null) }), "Venue");
    }

    [Fact]
    [Trait("Spec", "SPEC-0023:UT-02")]
    public async Task AFreeVenueNameOfOneCharacterFails()
    {
        ShouldFailOn(await ValidateAsync(Valid() with { Venue = new ShowVenueInput(null, "X") }), "Venue");
    }
}
