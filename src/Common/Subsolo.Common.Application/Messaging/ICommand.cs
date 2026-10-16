namespace Subsolo.Common.Application.Messaging;

/// <summary>SPEC-0007 — base de todo comando (usada pelos decorators).</summary>
public interface IBaseCommand;

/// <summary>SPEC-0007 — comando sem valor de retorno (vertical slice: <c>Features/&lt;CasoDeUso&gt;/</c>).</summary>
public interface ICommand : IBaseCommand;

/// <summary>SPEC-0007 — comando que devolve <typeparamref name="TResponse"/> em caso de sucesso.</summary>
public interface ICommand<TResponse> : IBaseCommand;
