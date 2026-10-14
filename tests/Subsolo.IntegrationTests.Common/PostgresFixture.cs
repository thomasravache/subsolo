using Xunit;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — PostgreSQL 18 real (imagem <c>postgres:18.6</c>) num container efêmero, compartilhado por coleção
/// de testes (<see cref="PostgresCollection"/>). Entre testes, <see cref="ResetAsync"/> limpa as tabelas dos
/// schemas indicados com <c>TRUNCATE</c>.
/// </summary>
/// <remarks>
/// Sem Docker disponível, a inicialização falha logo com a mensagem
/// "Docker não encontrado: inicie o Docker para rodar testes de integração".
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>String de conexão do banco do container (válida depois de <see cref="InitializeAsync"/>).</summary>
    public string ConnectionString => throw new NotImplementedException();

    /// <summary>Esvazia (<c>TRUNCATE</c>) todas as tabelas dos schemas informados.</summary>
    public Task ResetAsync(params string[] schemas) => throw new NotImplementedException();

    public ValueTask InitializeAsync() => throw new NotImplementedException();

    public ValueTask DisposeAsync() => throw new NotImplementedException();
}
