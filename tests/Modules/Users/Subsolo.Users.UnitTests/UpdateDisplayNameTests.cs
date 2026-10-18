using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application;
using Subsolo.Users.UnitTests.Infrastructure;

namespace Subsolo.Users.UnitTests;

/// <summary>
/// SPEC-0019 UT-01 — a regra do nome de exibição (<c>PUT /api/v1/me</c>): "  Caio Grind  " é guardado sem os espaços
/// das pontas; <c>""</c> e <see langword="null"/> removem o nome; 60 caracteres passam; 61 caracteres e um nome feito só
/// de espaços e caracteres invisíveis dão <c>users.invalid_display_name</c> (ligado ao campo <c>displayName</c>) e nada
/// é gravado.
/// </summary>
public sealed class UpdateDisplayNameTests
{
    private const string Email = "fulano@email.com";

    [Fact]
    [Trait("Spec", "SPEC-0019:UT-01")]
    public async Task NameIsStoredWithoutTheSpacesAroundIt()
    {
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        var saves = harness.Base.UnitOfWork.Saves;

        var result = await harness.RenameAsync(user.Id, "  Caio Grind  ");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.DisplayName.ShouldBe("Caio Grind");
        result.Value.Id.ShouldBe(user.Id);
        result.Value.Email.ShouldBe(Email);
        result.Value.Roles.ShouldBe(user.Roles);
        user.DisplayName.ShouldBe("Caio Grind");
        harness.Base.UnitOfWork.Saves.ShouldBe(saves + 1, "o nome novo não foi gravado");
    }

    [Theory]
    [Trait("Spec", "SPEC-0019:UT-01")]
    [InlineData("")]
    [InlineData(null)]
    public async Task EmptyOrNullRemovesTheName(string? displayName)
    {
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        (await harness.RenameAsync(user.Id, "Caio Grind")).IsSuccess.ShouldBeTrue();
        var saves = harness.Base.UnitOfWork.Saves;

        var result = await harness.RenameAsync(user.Id, displayName);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.DisplayName.ShouldBeNull();
        user.DisplayName.ShouldBeNull();
        harness.Base.UnitOfWork.Saves.ShouldBe(saves + 1, "a remoção do nome não foi gravada");
    }

    [Fact]
    [Trait("Spec", "SPEC-0019:UT-01")]
    public async Task SixtyCharactersAreAccepted()
    {
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        var name = new string('a', 60);

        var result = await harness.RenameAsync(user.Id, $"  {name}  ");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.DisplayName.ShouldBe(name);
        user.DisplayName.ShouldBe(name);
    }

    [Theory]
    [Trait("Spec", "SPEC-0019:UT-01")]
    [InlineData(61)]
    [InlineData(0)]
    public async Task SixtyOneCharactersOrOnlyInvisibleTextIsInvalidAndNothingIsSaved(int length)
    {
        // 0 = só espaços e caracteres invisíveis (espaço de largura zero), que não somem no trim mas não mostram nada.
        var invalid = length > 0 ? new string('a', length) : "​ ​";
        var harness = new AccountHarness();
        var user = await harness.SignInAsync(Email);
        (await harness.RenameAsync(user.Id, "Caio Grind")).IsSuccess.ShouldBeTrue();
        var saves = harness.Base.UnitOfWork.Saves;

        var result = await harness.RenameAsync(user.Id, invalid);

        result.IsFailure.ShouldBeTrue($"\"{invalid}\" deveria ser recusado");
        result.Error.Code.ShouldBe(UsersErrors.InvalidDisplayName);
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.ValidationErrors.Keys.ShouldContain("displayName", "o erro não está ligado ao campo displayName");
        user.DisplayName.ShouldBe("Caio Grind", "o nome antigo continua");
        harness.Base.UnitOfWork.Saves.ShouldBe(saves, "nada é gravado com nome inválido");
    }
}
