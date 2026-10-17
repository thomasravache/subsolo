using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Users.Application.Features.SignOut;

/// <summary>SPEC-0014 — logout: revoga no servidor a sessão do token do cookie.</summary>
public sealed record SignOutCommand(string SessionToken) : ICommand;
