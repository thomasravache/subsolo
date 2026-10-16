// SPEC-0007 — modelo da aplicação no Aspire (ADR-0004): `aspire run` sobe PostgreSQL 18, Seq, Mailpit, a API e o front.
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Sob o Aspire.Hosting.Testing (portas aleatórias), cada execução gera uma senha nova para o PostgreSQL: o volume
// persistente, criado com a senha de outra execução, recusaria a conexão. Nos testes o banco é descartável.
var isolatedRun = builder.Configuration.GetValue<bool>("DcpPublisher:RandomizePorts");

var postgres = builder.AddPostgres("postgres").WithImageTag("18.6");
if (!isolatedRun)
{
    postgres.WithDataVolume("subsolo-pg");
}

var database = postgres.AddDatabase("subsolo");

var seq = builder.AddSeq("seq");

var mailpit = builder.AddContainer("mailpit", "axllent/mailpit", "v1.31.3")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp", scheme: "tcp");

var api = builder.AddProject<Projects.Subsolo_Api>("api")
    .WithReference(database)
    .WithReference(seq)
    .WithReference(mailpit.GetEndpoint("smtp"))
    .WaitFor(database)
    .WithHttpHealthCheck("/health", endpointName: "http");

builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/");

await builder.Build().RunAsync();
