using FluentValidation.TestHelper;
using Subsolo.Modules.Moderation.Application;
using Subsolo.Modules.Moderation.Application.Features.ReportShow;
using Subsolo.Modules.Moderation.Domain.Reports;

namespace Subsolo.Moderation.UnitTests;

/// <summary>
/// SPEC-0033 UT-01 — o validador da denúncia: cada um dos quatro motivos do §6 passa (com ou sem detalhe; <c>other</c>
/// com detalhe), e o detalhe com 500 caracteres passa; <c>other</c> sem detalhe (nulo, vazio ou só espaços), detalhe com
/// 501 caracteres e motivo desconhecido (ou ausente) dão erro no campo (<c>Details</c> ou <c>Reason</c>) com
/// <c>moderation.invalid_report</c> no <c>ErrorCode</c>.
/// </summary>
public sealed class ReportShowValidatorTests
{
    private const string InvalidReport = ModerationErrors.InvalidReport;

    private static ReportShowCommand Command(string? reason, string? details = null) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), reason, details);

    private static Task<TestValidationResult<ReportShowCommand>> ValidateAsync(ReportShowCommand command) =>
        new ReportShowCommandValidator().TestValidateAsync(command, cancellationToken: TestContext.Current.CancellationToken);

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData(ReportReasons.Fake)]
    [InlineData(ReportReasons.CancelledUnannounced)]
    [InlineData(ReportReasons.WrongInfo)]
    public async Task EachReasonPassesWithoutDetails(string reason)
    {
        var result = await ValidateAsync(Command(reason));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData(ReportReasons.Fake)]
    [InlineData(ReportReasons.CancelledUnannounced)]
    [InlineData(ReportReasons.WrongInfo)]
    [InlineData(ReportReasons.Other)]
    public async Task EachReasonPassesWithDetails(string reason)
    {
        var result = await ValidateAsync(Command(reason, "A casa postou que o show foi adiado para dezembro."));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OtherWithoutDetailsFailsOnDetails(string? details)
    {
        var result = await ValidateAsync(Command(ReportReasons.Other, details));

        result.ShouldHaveValidationErrorFor(command => command.Details).WithErrorCode(InvalidReport);
        result.ShouldNotHaveValidationErrorFor(command => command.Reason);
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData(ReportReasons.WrongInfo)]
    [InlineData(ReportReasons.Other)]
    public async Task DetailsWith500CharactersPass(string reason)
    {
        var result = await ValidateAsync(Command(reason, new string('a', ReportReasons.DetailsMaxLength)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData(ReportReasons.WrongInfo)]
    [InlineData(ReportReasons.Other)]
    public async Task DetailsWith501CharactersFailOnDetails(string reason)
    {
        var result = await ValidateAsync(Command(reason, new string('a', ReportReasons.DetailsMaxLength + 1)));

        result.ShouldHaveValidationErrorFor(command => command.Details).WithErrorCode(InvalidReport);
    }

    [Theory]
    [Trait("Spec", "SPEC-0033:UT-01")]
    [InlineData("spam")]
    [InlineData("FAKE")]
    [InlineData("")]
    [InlineData(null)]
    public async Task AnUnknownReasonFailsOnReason(string? reason)
    {
        var result = await ValidateAsync(Command(reason, "Qualquer coisa."));

        result.ShouldHaveValidationErrorFor(command => command.Reason).WithErrorCode(InvalidReport);
    }
}
