using FluentValidation.TestHelper;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.RequestProfile;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Profiles.UnitTests.Infrastructure;

namespace Subsolo.Profiles.UnitTests;

/// <summary>
/// SPEC-0021 UT-02 — o validador do pedido (com o catálogo de teste): nome com 1 e 81 caracteres, cidade ou estilo que o
/// catálogo nega, estilos em perfil de casa (e de produtor), 0 ou 6 estilos numa banda, links
/// <c>http://instagram.com/x</c> e <c>https://evil.com/x</c> (e o link de um domínio no campo de outro), tipo
/// desconhecido e contato fora de 3–200 dão erro no campo com <c>profiles.invalid_request</c> no <c>ErrorCode</c>; só
/// <c>https://www.instagram.com/podridao</c> (e os links oficiais do Bandcamp e do Metal Archives) passam.
/// </summary>
public sealed class RequestProfileValidatorTests
{
    private const string InvalidRequest = ProfilesErrors.InvalidRequest;

    private readonly ProfilesHarness _harness = new();

    private static RequestProfileCommand Valid(
        string? type = ProfileTypes.Band,
        string? name = "Podridão",
        string? city = "curitiba",
        IReadOnlyList<string>? styles = null,
        ProfileLinksInput? links = null,
        string? contact = "@podridao no Instagram") =>
        new(Guid.CreateVersion7(), type, name, city, styles ?? ["death"], links, contact);

    private Task<TestValidationResult<RequestProfileCommand>> ValidateAsync(RequestProfileCommand command) =>
        _harness.Create<RequestProfileCommandValidator>().TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task AValidBandRequestPasses()
    {
        var result = await ValidateAsync(Valid(
            styles: ["death", "grind"],
            links: new ProfileLinksInput(
                "https://www.instagram.com/podridao",
                "https://podridao.bandcamp.com",
                "https://www.metal-archives.com/bands/Podrid%C3%A3o/3540000000")));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(ProfileTypes.Venue)]
    [InlineData(ProfileTypes.Producer)]
    public async Task VenueAndProducerWithoutStylesPass(string type)
    {
        var result = await ValidateAsync(Valid(type: type, name: "Porão do Rock", styles: []));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(1)]
    [InlineData(81)]
    public async Task NameOutside2To80CharactersFails(int length)
    {
        var result = await ValidateAsync(Valid(name: new string('a', length)));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(2)]
    [InlineData(80)]
    public async Task NameWith2Or80CharactersPasses(int length)
    {
        var result = await ValidateAsync(Valid(name: new string('a', length)));

        result.ShouldNotHaveValidationErrorFor(command => command.Name);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingNameFails(string? name)
    {
        var result = await ValidateAsync(Valid(name: name));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("tribute")]
    [InlineData("Band")]
    public async Task UnknownTypeFails(string? type)
    {
        var result = await ValidateAsync(Valid(type: type));

        result.ShouldHaveValidationErrorFor(command => command.Type).WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData("atlantida")]
    [InlineData("")]
    [InlineData(null)]
    public async Task CityTheCatalogDeniesFails(string? city)
    {
        var result = await ValidateAsync(Valid(city: city));

        result.ShouldHaveValidationErrorFor(command => command.City).WithErrorCode(InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task TheCityIsCheckedInTheCatalog()
    {
        await ValidateAsync(Valid());

        _harness.Catalog.CityQueries.ShouldContain("curitiba", "a cidade é conferida no IEventsCatalog");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task StyleTheCatalogDeniesFails()
    {
        var result = await ValidateAsync(Valid(styles: ["death", "sertanejo"]));

        result.ShouldHaveValidationErrorFor(command => command.Styles).WithErrorCode(InvalidRequest);
        _harness.Catalog.StyleQueries.ShouldNotBeEmpty("os estilos são conferidos no IEventsCatalog");
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(ProfileTypes.Venue)]
    [InlineData(ProfileTypes.Producer)]
    public async Task StylesOutsideABandFail(string type)
    {
        var result = await ValidateAsync(Valid(type: type, name: "Porão do Rock", styles: ["death"]));

        result.ShouldHaveValidationErrorFor(command => command.Styles).WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ABandNeeds1To5Styles(int count)
    {
        string[] all = ["death", "black", "grind", "doom", "thrash", "crust"];

        var result = await ValidateAsync(Valid(styles: [.. all.Take(count)]));

        result.ShouldHaveValidationErrorFor(command => command.Styles).WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(1)]
    [InlineData(5)]
    public async Task ABandWith1Or5StylesPasses(int count)
    {
        string[] all = ["death", "black", "grind", "doom", "thrash"];

        var result = await ValidateAsync(Valid(styles: [.. all.Take(count)]));

        result.ShouldNotHaveValidationErrorFor(command => command.Styles);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData("http://instagram.com/x")]
    [InlineData("https://evil.com/x")]
    [InlineData("https://instagram.com.evil.com/x")]
    [InlineData("https://podridao.bandcamp.com")]
    [InlineData("instagram.com/podridao")]
    [InlineData("javascript:alert(1)")]
    public async Task AnInstagramLinkOutsideHttpsInstagramFails(string link)
    {
        var result = await ValidateAsync(Valid(links: new ProfileLinksInput(link, null, null)));

        result.ShouldHaveValidationErrorFor("Links.Instagram").WithErrorCode(InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task OnlyTheHttpsInstagramLinkPasses()
    {
        var result = await ValidateAsync(Valid(links: new ProfileLinksInput("https://www.instagram.com/podridao", null, null)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData("http://podridao.bandcamp.com")]
    [InlineData("https://evil.com/x")]
    [InlineData("https://www.instagram.com/podridao")]
    public async Task ABandcampLinkOutsideHttpsBandcampFails(string link)
    {
        var result = await ValidateAsync(Valid(links: new ProfileLinksInput(null, link, null)));

        result.ShouldHaveValidationErrorFor("Links.Bandcamp").WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData("http://www.metal-archives.com/bands/x/1")]
    [InlineData("https://evil.com/x")]
    [InlineData("https://podridao.bandcamp.com")]
    public async Task AMetalArchivesLinkOutsideHttpsMetalArchivesFails(string link)
    {
        var result = await ValidateAsync(Valid(links: new ProfileLinksInput(null, null, link)));

        result.ShouldHaveValidationErrorFor("Links.MetalArchives").WithErrorCode(InvalidRequest);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-02")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]
    public async Task ContactShorterThan3Fails(string? contact)
    {
        var result = await ValidateAsync(Valid(contact: contact));

        result.ShouldHaveValidationErrorFor(command => command.Contact).WithErrorCode(InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task ContactLongerThan200Fails()
    {
        var result = await ValidateAsync(Valid(contact: new string('c', 201)));

        result.ShouldHaveValidationErrorFor(command => command.Contact).WithErrorCode(InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-02")]
    public async Task EveryFailureCarriesTheInvalidRequestCodeAndItsField()
    {
        var result = await ValidateAsync(new RequestProfileCommand(
            Guid.CreateVersion7(),
            ProfileTypes.Venue,
            "a",
            "atlantida",
            ["death"],
            new ProfileLinksInput("http://instagram.com/x", "https://evil.com/x", null),
            "ab"));

        result.Errors.ShouldNotBeEmpty();
        result.Errors.ShouldAllBe(failure => failure.ErrorCode == InvalidRequest);
        result.Errors.Select(failure => failure.PropertyName).Distinct(StringComparer.Ordinal).ShouldBe(
            ["Name", "City", "Styles", "Links.Instagram", "Links.Bandcamp", "Contact"],
            ignoreOrder: true,
            "um erro por campo inválido, cada um ligado ao campo");
    }
}
