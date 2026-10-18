using FluentValidation.TestHelper;
using Microsoft.Extensions.Logging.Abstractions;
using Subsolo.Common.Domain;
using Subsolo.Modules.Events.Application;
using Subsolo.Modules.Events.Application.Features.CreateStyle;
using Subsolo.Modules.Events.Application.Features.RenameStyle;
using Subsolo.Modules.Events.Domain.Styles;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0017 UT-03 — nome de estilo com 1 ou 31 caracteres → <c>events.invalid_style</c> (criar e renomear);
/// "Black" com <c>black</c> já cadastrado → <c>events.style_taken</c> (Conflict) sem gravar nada; renomear mantém o slug.
/// </summary>
public sealed class StyleTests
{
    private const string InvalidStyle = "events.invalid_style";

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-03")]
    [InlineData(1)]
    [InlineData(31)]
    public void CreateWithNameOutside2To30CharactersFailsWithInvalidStyle(int length)
    {
        var result = new CreateStyleCommandValidator().TestValidate(new CreateStyleCommand(new string('a', length)));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidStyle);
    }

    [Theory]
    [Trait("Spec", "SPEC-0017:UT-03")]
    [InlineData(1)]
    [InlineData(31)]
    public void RenameWithNameOutside2To30CharactersFailsWithInvalidStyle(int length)
    {
        var result = new RenameStyleCommandValidator().TestValidate(new RenameStyleCommand("crust", new string('a', length)));

        result.ShouldHaveValidationErrorFor(command => command.Name).WithErrorCode(InvalidStyle);
    }

    // Guarda: o validador ainda vazio já aceita os nomes no limite; continua passando depois das regras.
    [Theory]
    [Trait("Spec", "SPEC-0017:UT-03")]
    [InlineData(2)]
    [InlineData(30)]
    public void NamesWith2To30CharactersPass(int length)
    {
        var name = new string('a', length);

        new CreateStyleCommandValidator().TestValidate(new CreateStyleCommand(name)).ShouldNotHaveAnyValidationErrors();
        new RenameStyleCommandValidator().TestValidate(new RenameStyleCommand("crust", name)).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:UT-03")]
    public async Task CreatingANameWhoseSlugExistsFailsWithStyleTakenAndSavesNothing()
    {
        var styles = new InMemoryStyles(Style.Create("black", "Black"));
        var unitOfWork = new CountingUnitOfWork();
        var handler = new CreateStyleCommandHandler(styles, unitOfWork, NullLogger<CreateStyleCommandHandler>.Instance);

        var result = await handler.Handle(new CreateStyleCommand("Black"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("events.style_taken");
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        styles.Added.ShouldBeEmpty();
        unitOfWork.Saves.ShouldBe(0);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:UT-03")]
    public async Task CreatingANewNameAddsTheStyleWithTheGeneratedSlug()
    {
        var styles = new InMemoryStyles(Style.Create("black", "Black"));
        var unitOfWork = new CountingUnitOfWork();
        var handler = new CreateStyleCommandHandler(styles, unitOfWork, NullLogger<CreateStyleCommandHandler>.Instance);

        var result = await handler.Handle(new CreateStyleCommand("Crust Punk"), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : string.Empty);
        result.Value.Slug.ShouldBe("crust-punk");
        result.Value.Name.ShouldBe("Crust Punk");
        styles.Added.Select(style => style.Slug).ShouldBe(["crust-punk"]);
        unitOfWork.Saves.ShouldBe(1);
    }

    [Fact]
    [Trait("Spec", "SPEC-0017:UT-03")]
    public void RenameChangesTheNameAndKeepsTheSlug()
    {
        var style = Style.Create("crust", "Crust");

        style.Rename("Crust Punk");

        style.Slug.ShouldBe("crust");
        style.Name.ShouldBe("Crust Punk");
    }

    /// <summary>Dublê: estilos em memória; guarda os incluídos por <see cref="Add"/>.</summary>
    private sealed class InMemoryStyles(params Style[] existing) : IStyleRepository
    {
        private readonly List<Style> _styles = [.. existing];

        public List<Style> Added { get; } = [];

        public Task<Style?> GetBySlugAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(_styles.SingleOrDefault(style => style.Slug == slug));

        public Task<IReadOnlyList<Style>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Style>>([.. _styles.OrderBy(style => style.Name, StringComparer.Ordinal)]);

        public void Add(Style style)
        {
            _styles.Add(style);
            Added.Add(style);
        }
    }

    /// <summary>Dublê: unidade de trabalho que só conta as gravações.</summary>
    private sealed class CountingUnitOfWork : IEventsUnitOfWork
    {
        public int Saves { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }
}
