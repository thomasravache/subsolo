using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Template.Application.Features.RenameTemplateItem;

/// <summary>Dublê de teste: comando sem retorno (vertical slice), com validador.</summary>
public sealed record RenameTemplateItemCommand(Guid ItemId, string Name) : ICommand;
