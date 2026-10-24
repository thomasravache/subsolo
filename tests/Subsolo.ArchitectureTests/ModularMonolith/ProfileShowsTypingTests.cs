using Shouldly;
using Xunit;

namespace Subsolo.ArchitectureTests.ModularMonolith;

/// <summary>
/// SPEC-0031 H26-01 (item herdado da revisão da SPEC-0026) — os shows do perfil são tipados como
/// <c>ProfileShowResponse</c> de ponta a ponta: <c>IShowsReadModel.ListByProfileAsync</c> devolve
/// <c>IReadOnlyList&lt;ProfileShowResponse&gt;</c>, <c>ProfileShowsResponse.Shows</c> é
/// <c>IReadOnlyList&lt;ProfileShowResponse&gt;</c> e o <c>ListProfileShowsEndpoint</c> (Events.Presentation) não faz
/// <c>.Cast&lt;ProfileShowResponse&gt;()</c>. As respostas da API não mudam (a suíte do SPEC-0026 continua valendo).
/// </summary>
public sealed class ProfileShowsTypingTests
{
    private const string Application = "Subsolo.Modules.Events.Application";

    private const string ProfileShowResponse = "Subsolo.Modules.Events.Application.Features.ListProfileShows.ProfileShowResponse";

    [Fact]
    [Trait("Spec", "SPEC-0031:H26-01")]
    [Trait("Kind", "Integration")]
    public void TheReadModelReturnsProfileShowResponses()
    {
        var readModel = MemberReferences.Module(Application).GetType("Subsolo.Modules.Events.Application.IShowsReadModel", throwOnError: true)!;

        var method = readModel.GetMethod("ListByProfileAsync").ShouldNotBeNull("IShowsReadModel.ListByProfileAsync sumiu");

        Describe(method.ReturnType).ShouldBe($"Task<IReadOnlyList<{ProfileShowResponse}>>");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H26-01")]
    [Trait("Kind", "Integration")]
    public void TheQueryResponseHoldsProfileShowResponses()
    {
        var response = MemberReferences.Module(Application)
            .GetType("Subsolo.Modules.Events.Application.Features.ListProfileShows.ProfileShowsResponse", throwOnError: true)!;

        var shows = response.GetProperty("Shows").ShouldNotBeNull("ProfileShowsResponse.Shows sumiu");

        Describe(shows.PropertyType).ShouldBe($"IReadOnlyList<{ProfileShowResponse}>");
    }

    [Fact]
    [Trait("Spec", "SPEC-0031:H26-01")]
    [Trait("Kind", "Integration")]
    public void TheEndpointDoesNotCastTheShows()
    {
        var casts = MemberReferences.CalledOn(MemberReferences.Module("Subsolo.Modules.Events.Presentation"), "System.Linq", "Enumerable")
            .Where(name => name == "Cast")
            .ToList();

        casts.ShouldBeEmpty("o Events.Presentation ainda faz .Cast<ProfileShowResponse>() nos shows do perfil");
    }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`', StringComparison.Ordinal)]}<{string.Join(",", type.GetGenericArguments().Select(Describe))}>"
            : type.FullName ?? type.Name;
}
