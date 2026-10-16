using FluentValidation.TestHelper;
using Subsolo.Modules.Events.Application.Features.GetCityAgenda;

namespace Subsolo.Events.UnitTests;

/// <summary>
/// SPEC-0008 UT-02 — slug fora de <c>^[a-z0-9-]{2,60}$</c> → <c>agenda.invalid_slug</c>; <c>to &lt; from</c> ou
/// período de mais de 180 dias → <c>agenda.invalid_range</c> (o código vai no <c>ErrorCode</c> da falha).
/// </summary>
public sealed class GetCityAgendaQueryValidatorTests
{
    private static readonly DateOnly From = new(2026, 10, 13);

    private readonly GetCityAgendaQueryValidator _validator = new();

    [Theory]
    [Trait("Spec", "SPEC-0008:UT-02")]
    [InlineData("Cidade-Exemplo")]
    [InlineData("cidade_exemplo")]
    [InlineData("Cidade_X")]
    [InlineData("a")]
    [InlineData("")]
    public void InvalidSlugFailsWithInvalidSlugCode(string slug)
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery(slug));

        result.ShouldHaveValidationErrorFor(query => query.Slug).WithErrorCode("agenda.invalid_slug");
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public void SlugLongerThan60CharactersFailsWithInvalidSlugCode()
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery(new string('a', 61)));

        result.ShouldHaveValidationErrorFor(query => query.Slug).WithErrorCode("agenda.invalid_slug");
    }

    [Theory]
    [Trait("Spec", "SPEC-0008:UT-02")]
    [InlineData("cidade-exemplo")]
    [InlineData("sp")]
    [InlineData("sao-paulo-2")]
    public void ValidSlugWithoutPeriodPasses(string slug)
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery(slug));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public void ToBeforeFromFailsWithInvalidRangeCode()
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery("cidade-exemplo", From, From.AddDays(-1)));

        result.Errors.ShouldContain(failure => failure.ErrorCode == "agenda.invalid_range", Describe(result));
        result.Errors.ShouldNotContain(failure => failure.ErrorCode == "agenda.invalid_slug");
    }

    [Fact]
    [Trait("Spec", "SPEC-0008:UT-02")]
    public void PeriodOf181DaysFailsWithInvalidRangeCode()
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery("cidade-exemplo", From, From.AddDays(181)));

        result.Errors.ShouldContain(failure => failure.ErrorCode == "agenda.invalid_range", Describe(result));
    }

    [Theory]
    [Trait("Spec", "SPEC-0008:UT-02")]
    [InlineData(0)]
    [InlineData(60)]
    [InlineData(180)]
    public void PeriodUpTo180DaysPasses(int days)
    {
        var result = _validator.TestValidate(new GetCityAgendaQuery("cidade-exemplo", From, From.AddDays(days)));

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static string Describe(TestValidationResult<GetCityAgendaQuery> result) =>
        string.Join("; ", result.Errors.Select(failure => $"{failure.PropertyName}: {failure.ErrorCode}"));
}
