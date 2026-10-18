using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.DeleteAccount;

/// <summary>
/// SPEC-0019 — <c>DELETE /api/v1/me</c>: exclui a conta de <paramref name="UserId"/> se <paramref name="ConfirmEmail"/>
/// for o e-mail dela (sem diferenciar maiúsculas e espaços nas pontas).
/// </summary>
public sealed record DeleteAccountCommand(Guid UserId, string? ConfirmEmail) : ICommand;
