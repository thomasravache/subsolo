using FluentValidation.TestHelper;
using Subsolo.Common.Domain;
using Subsolo.Modules.Profiles.Application;
using Subsolo.Modules.Profiles.Application.Features.ApproveProfileRequest;
using Subsolo.Modules.Profiles.Application.Features.RejectProfileRequest;
using Subsolo.Modules.Profiles.Domain.Profiles;
using Subsolo.Modules.Profiles.Domain.Requests;
using Subsolo.Profiles.UnitTests.Infrastructure;

namespace Subsolo.Profiles.UnitTests;

/// <summary>
/// SPEC-0021 UT-03 — dados pedidos <c>new</c> e <c>claim</c> pendentes, quando aprovados, o perfil fica aprovado com o
/// dono (o novo é criado com os dados do pedido; o sem dono ganha o dono) e é gerado <c>ProfileOwnerAssigned</c> uma
/// vez; aprovar de novo (ou aprovar um recusado) dá <c>profiles.request_decided</c>; <c>claim</c> de perfil que ganhou
/// dono no meio dá <c>profiles.already_owned</c>; pedido inexistente, <c>profiles.request_not_found</c>; recusar sem
/// motivo é erro de validação no campo <c>Reason</c>, e recusar com motivo grava a recusa, o motivo e a data.
/// </summary>
public sealed class ApproveAndRejectTests
{
    private static readonly Guid Fan = Guid.CreateVersion7();
    private static readonly Guid OtherFan = Guid.CreateVersion7();

    private static Task<Result<ProfileRequestApproved>> ApproveAsync(ProfilesHarness harness, Guid requestId) =>
        harness.Create<ApproveProfileRequestCommandHandler>()
            .Handle(new ApproveProfileRequestCommand(requestId), TestContext.Current.CancellationToken);

    private static Task<Result> RejectAsync(ProfilesHarness harness, Guid requestId, string? reason) =>
        harness.Create<RejectProfileRequestCommandHandler>()
            .Handle(new RejectProfileRequestCommand(requestId, reason), TestContext.Current.CancellationToken);

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ApprovingANewRequestCreatesTheApprovedProfileWithTheOwnerAndRaisesOwnerAssigned()
    {
        var harness = new ProfilesHarness();
        var draft = ProfilesHarness.Draft() with
        {
            Links = new ProfileLinks("https://www.instagram.com/podridao", null, null),
        };
        var request = harness.AddNewRequest(Fan, draft);
        harness.Clock.Advance(TimeSpan.FromHours(3));

        var result = await ApproveAsync(harness, request.Id);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        var profile = harness.Profiles.All.ShouldHaveSingleItem("a aprovação de um pedido novo cria o perfil");
        profile.Id.ShouldBe(result.Value.ProfileId);
        profile.OwnerUserId.ShouldBe(Fan);
        profile.Type.ShouldBe(ProfileTypes.Band);
        profile.Name.ShouldBe("Podridão");
        profile.Slug.ShouldBe("podridao");
        profile.City.ShouldBe("curitiba");
        profile.Styles.ShouldBe(["death"]);
        profile.Links.Instagram.ShouldBe("https://www.instagram.com/podridao");
        request.Status.ShouldBe(ProfileRequestStatuses.Approved);
        request.ProfileId.ShouldBe(profile.Id);
        request.DecidedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        harness.UnitOfWork.Saves.ShouldBe(1, "a aprovação é gravada numa transação");
        var assigned = harness.UnitOfWork.Events.OfType<ProfileOwnerAssignedDomainEvent>()
            .ShouldHaveSingleItem("ProfileOwnerAssigned não foi gerado (ou foi mais de uma vez)");
        assigned.ProfileId.ShouldBe(profile.Id);
        assigned.UserId.ShouldBe(Fan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ApprovingAClaimGivesTheOwnerlessProfileItsOwnerAndRaisesOwnerAssigned()
    {
        var harness = new ProfilesHarness();
        var profile = harness.AddProfile(ProfilesHarness.Draft("Porão do Rock", "porao-do-rock", ProfileTypes.Venue));
        var claim = harness.AddClaim(Fan, profile.Id);

        var result = await ApproveAsync(harness, claim.Id);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        result.Value.ProfileId.ShouldBe(profile.Id);
        harness.Profiles.All.ShouldHaveSingleItem("a reivindicação não cria perfil novo");
        profile.OwnerUserId.ShouldBe(Fan);
        claim.Status.ShouldBe(ProfileRequestStatuses.Approved);
        claim.DecidedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        var assigned = harness.UnitOfWork.Events.OfType<ProfileOwnerAssignedDomainEvent>()
            .ShouldHaveSingleItem("ProfileOwnerAssigned não foi gerado (ou foi mais de uma vez)");
        assigned.ProfileId.ShouldBe(profile.Id);
        assigned.UserId.ShouldBe(Fan);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ApprovingTwiceReturnsRequestDecidedAndRaisesNothingMore()
    {
        var harness = new ProfilesHarness();
        var request = harness.AddNewRequest(Fan, ProfilesHarness.Draft());
        (await ApproveAsync(harness, request.Id)).IsSuccess.ShouldBeTrue();
        var events = harness.UnitOfWork.Events.Count;

        var again = await ApproveAsync(harness, request.Id);

        again.IsFailure.ShouldBeTrue("o pedido já foi aprovado");
        again.Error.Code.ShouldBe(ProfilesErrors.RequestDecided);
        again.Error.Type.ShouldBe(ErrorType.Conflict);
        harness.Profiles.All.Count.ShouldBe(1, "a segunda aprovação não cria outro perfil");
        harness.UnitOfWork.Events.Count.ShouldBe(events, "a segunda aprovação não gera evento");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ApprovingARejectedRequestReturnsRequestDecided()
    {
        var harness = new ProfilesHarness();
        var request = harness.AddNewRequest(Fan, ProfilesHarness.Draft());
        (await RejectAsync(harness, request.Id, "Banda sem material publicado.")).IsSuccess.ShouldBeTrue();

        var result = await ApproveAsync(harness, request.Id);

        result.IsFailure.ShouldBeTrue("o pedido já foi recusado");
        result.Error.Code.ShouldBe(ProfilesErrors.RequestDecided);
        harness.Profiles.All.ShouldBeEmpty();
        request.Status.ShouldBe(ProfileRequestStatuses.Rejected);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ClaimOfAProfileThatGotAnOwnerMeanwhileReturnsAlreadyOwned()
    {
        var harness = new ProfilesHarness();
        var profile = harness.AddProfile(ProfilesHarness.Draft("Porão do Rock", "porao-do-rock", ProfileTypes.Venue));
        var first = harness.AddClaim(Fan, profile.Id);
        var second = harness.AddClaim(OtherFan, profile.Id);
        (await ApproveAsync(harness, first.Id)).IsSuccess.ShouldBeTrue();
        var events = harness.UnitOfWork.Events.Count;

        var result = await ApproveAsync(harness, second.Id);

        result.IsFailure.ShouldBeTrue("o perfil ganhou dono antes desta aprovação");
        result.Error.Code.ShouldBe(ProfilesErrors.AlreadyOwned);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        profile.OwnerUserId.ShouldBe(Fan, "o primeiro dono continua");
        second.Status.ShouldNotBe(ProfileRequestStatuses.Approved);
        harness.UnitOfWork.Events.Count.ShouldBe(events, "nenhum evento novo");
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ApprovingAnUnknownRequestReturnsRequestNotFound()
    {
        var harness = new ProfilesHarness();

        var result = await ApproveAsync(harness, Guid.CreateVersion7());

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ProfilesErrors.RequestNotFound);
        result.Error.Type.ShouldBe(ErrorType.NotFound);
    }

    [Theory]
    [Trait("Spec", "SPEC-0021:UT-03")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    public async Task RejectingWithoutAReasonIsAValidationError(string? reason)
    {
        var harness = new ProfilesHarness();
        var validator = harness.Create<RejectProfileRequestCommandValidator>();

        var result = await validator.TestValidateAsync(
            new RejectProfileRequestCommand(Guid.CreateVersion7(), reason), cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(command => command.Reason).WithErrorCode(ProfilesErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task ReasonLongerThan200IsAValidationError()
    {
        var harness = new ProfilesHarness();
        var validator = harness.Create<RejectProfileRequestCommandValidator>();

        var result = await validator.TestValidateAsync(
            new RejectProfileRequestCommand(Guid.CreateVersion7(), new string('m', 201)),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldHaveValidationErrorFor(command => command.Reason).WithErrorCode(ProfilesErrors.InvalidRequest);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task RejectingWithAReasonRecordsTheRejectionTheReasonAndTheDate()
    {
        var harness = new ProfilesHarness();
        var request = harness.AddNewRequest(Fan, ProfilesHarness.Draft());
        harness.Clock.Advance(TimeSpan.FromDays(1));

        var result = await RejectAsync(harness, request.Id, "Banda sem material publicado.");

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Code : null);
        request.Status.ShouldBe(ProfileRequestStatuses.Rejected);
        request.Reason.ShouldBe("Banda sem material publicado.");
        request.DecidedAtUtc.ShouldBe(harness.Clock.GetUtcNow());
        harness.Profiles.All.ShouldBeEmpty("recusar não cria perfil");
        harness.UnitOfWork.Saves.ShouldBe(1);
        harness.UnitOfWork.Events.OfType<ProfileOwnerAssignedDomainEvent>().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task RejectingADecidedRequestReturnsRequestDecided()
    {
        var harness = new ProfilesHarness();
        var request = harness.AddNewRequest(Fan, ProfilesHarness.Draft());
        (await ApproveAsync(harness, request.Id)).IsSuccess.ShouldBeTrue();

        var result = await RejectAsync(harness, request.Id, "Mudei de ideia.");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ProfilesErrors.RequestDecided);
        request.Status.ShouldBe(ProfileRequestStatuses.Approved);
    }

    [Fact]
    [Trait("Spec", "SPEC-0021:UT-03")]
    public async Task RejectingAnUnknownRequestReturnsRequestNotFound()
    {
        var harness = new ProfilesHarness();

        var result = await RejectAsync(harness, Guid.CreateVersion7(), "Motivo qualquer.");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(ProfilesErrors.RequestNotFound);
    }
}
