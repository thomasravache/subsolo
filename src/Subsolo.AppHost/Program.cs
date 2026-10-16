// SPEC-0007 — modelo da aplicação no Aspire (ADR-0004): `aspire run` sobe PostgreSQL 18, Seq, Mailpit, a API e o front.
// SPEC-0008 — `aspire publish -o artifacts/compose` gera o Docker Compose da mesma aplicação, com o Caddy na frente.
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

// Ambiente de publicação: docker-compose.yaml + .env (ADR-0004).
builder.AddDockerComposeEnvironment("compose");

// `Subsolo:EphemeralDatabase=true` (usado pelos testes do AppHost) sobe o PostgreSQL sem volume: cada execução de
// teste gera uma senha nova, e um volume persistente criado com a senha de outra execução recusaria a conexão.
var ephemeralDatabase = builder.Configuration.GetValue<bool>("Subsolo:EphemeralDatabase");

var postgres = builder.AddPostgres("postgres").WithImageTag("18.6");
if (!ephemeralDatabase)
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

var web = builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/");

if (builder.ExecutionContext.IsPublishMode)
{
    // Ambiente da API no Compose: Production por padrão (migrações pelo pipeline, ADR-0002). O Compose local
    // (tools/compose-up.sh) usa Development, que aplica as migrações e o seed da cidade de exemplo no start.
    api.WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.AddParameter("api-environment", "Production", publishValueAsDefault: true));

    // A API só sobe com o PostgreSQL aceitando conexões (no Compose, `depends_on` sozinho espera só o contêiner).
    postgres.PublishAsDockerComposeService((_, service) => service.Healthcheck = new Healthcheck
    {
        Test = ["CMD-SHELL", "pg_isready -U postgres"],
        Interval = "2s",
        Timeout = "5s",
        Retries = 30,
        StartPeriod = "5s",
    });
    api.PublishAsDockerComposeService((_, service) =>
        service.DependsOn[postgres.Resource.Name] = new ServiceDependency { Condition = "service_healthy" });

    // Caddy na frente (ADR-0004): /api/* e /health → api; o resto → web. Mesmo Caddyfile do deploy na VPS.
    // No Compose, o contêiner do web roda `next start` sobre o build de produção (Dockerfile gerado pelo Aspire).
#pragma warning disable ASPIREJAVASCRIPT001 // API em avaliação no Aspire 13.6; é o modo de publicação do Next.js sem `output: "standalone"`.
    web.PublishAsPackageScript("start");
#pragma warning restore ASPIREJAVASCRIPT001

    builder.AddContainer("caddy", "caddy", "2.11.7-alpine")
        .WithBindMount("../../infra/caddy/Caddyfile", "/etc/caddy/Caddyfile", isReadOnly: true)
        .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
        .WithExternalHttpEndpoints()
        .WithEnvironment("API_UPSTREAM", api.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WithEnvironment("WEB_UPSTREAM", web.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WaitFor(api)
        .WaitFor(web);
}

await builder.Build().RunAsync();
