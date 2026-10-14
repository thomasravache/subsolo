using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace Subsolo.IntegrationTests.Common;

/// <summary>
/// SPEC-0005 — coleção que compartilha um único <see cref="PostgresFixture"/> (um container) entre as classes de
/// teste marcadas com <c>[Collection(nameof(PostgresCollection))]</c>.
/// </summary>
[CollectionDefinition(nameof(PostgresCollection))]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Nome fixado pelo contrato da SPEC-0005 (convenção de coleção do xUnit).")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
