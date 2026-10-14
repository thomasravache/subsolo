using Subsolo.IntegrationTests.Common;

// SPEC-0005 IT-04 — exit 0: o fixture subiu; exit 1: falhou (a mensagem vai para o stderr).
var fixture = new PostgresFixture();
try
{
    await fixture.InitializeAsync();
    Console.WriteLine("PostgresFixture inicializado.");
    await fixture.DisposeAsync();
    return 0;
}
catch (Exception ex)
{
    await Console.Error.WriteLineAsync(ex.ToString());
    return 1;
}
