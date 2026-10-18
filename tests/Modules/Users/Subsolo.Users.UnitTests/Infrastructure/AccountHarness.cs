using Microsoft.Extensions.Logging.Abstractions;
using Subsolo.Common.Domain;
using Subsolo.Modules.Users.Application.Features.DeleteAccount;
using Subsolo.Modules.Users.Application.Features.ExportPersonalData;
using Subsolo.Modules.Users.Application.Features.GetMe;
using Subsolo.Modules.Users.Application.Features.UpdateDisplayName;
using Subsolo.Modules.Users.Domain.Users;
using Subsolo.Modules.Users.IntegrationEvents;

namespace Subsolo.Users.UnitTests.Infrastructure;

/// <summary>
/// SPEC-0019 — os handlers da "Minha conta" (<see cref="UpdateDisplayNameCommandHandler"/>,
/// <see cref="ExportPersonalDataQueryHandler"/>, <see cref="DeleteAccountCommandHandler"/>) sobre os dublês do
/// <see cref="UsersHarness"/> (usuários, sessões, unidade de trabalho que recolhe os eventos, relógio) e os links mágicos
/// em memória, com as <see cref="IPersonalDataSection"/> de teste informadas.
/// </summary>
internal sealed class AccountHarness
{
    public AccountHarness(params IPersonalDataSection[] sections)
    {
        UpdateDisplayName = new UpdateDisplayNameCommandHandler(
            Base.Users, Base.UnitOfWork, NullLogger<UpdateDisplayNameCommandHandler>.Instance);
        Export = new ExportPersonalDataQueryHandler(Base.Users, Base.Sessions, sections, Base.Clock);
        DeleteAccount = new DeleteAccountCommandHandler(
            Base.Users,
            Base.Sessions,
            MagicLinks,
            Base.UnitOfWork,
            NullLogger<DeleteAccountCommandHandler>.Instance);
    }

    public UsersHarness Base { get; } = new();

    public InMemoryMagicLinkTokens MagicLinks { get; } = new();

    public UpdateDisplayNameCommandHandler UpdateDisplayName { get; }

    public ExportPersonalDataQueryHandler Export { get; }

    public DeleteAccountCommandHandler DeleteAccount { get; }

    /// <summary>Entra com <paramref name="email"/> e devolve o usuário criado (ou o existente).</summary>
    public async Task<User> SignInAsync(string email)
    {
        var (result, _) = await Base.SignInAsync(email);
        return Base.Users.All.Single(user => user.Id == result.UserId);
    }

    public Task<Result<MeResponse>> RenameAsync(Guid userId, string? displayName) =>
        UpdateDisplayName.Handle(new UpdateDisplayNameCommand(userId, displayName), TestContext.Current.CancellationToken);

    public Task<Result<PersonalDataExport>> ExportAsync(Guid userId) =>
        Export.Handle(new ExportPersonalDataQuery(userId), TestContext.Current.CancellationToken);

    public Task<Result> DeleteAsync(Guid userId, string? confirmEmail) =>
        DeleteAccount.Handle(new DeleteAccountCommand(userId, confirmEmail), TestContext.Current.CancellationToken);
}

/// <summary>SPEC-0019 — dublê de <see cref="IPersonalDataSection"/> que devolve um valor fixo e guarda quem pediu.</summary>
internal sealed class FixedPersonalDataSection(string name, object? value) : IPersonalDataSection
{
    private readonly List<Guid> _requested = [];

    public string Name => name;

    public IReadOnlyList<Guid> Requested => _requested;

    public Task<object?> ExportAsync(Guid userId, CancellationToken ct)
    {
        _requested.Add(userId);
        return Task.FromResult(value);
    }
}
