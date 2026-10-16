using Subsolo.Common.Application.Messaging;

namespace Subsolo.Modules.Template.Application.Features.CreateTemplateNote;

/// <summary>Dublê de teste: comando com retorno (o id da nota criada), com validador.</summary>
public sealed record CreateTemplateNoteCommand(string Text) : ICommand<Guid>;
