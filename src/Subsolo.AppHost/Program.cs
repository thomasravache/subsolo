// SPEC-0007 — modelo da aplicação no Aspire (ADR-0004): `aspire run` sobe PostgreSQL 18, Seq, Mailpit, a API e o front.
// SPEC-0008 — `aspire publish -o artifacts/compose` gera o Docker Compose da mesma aplicação, com o Caddy na frente.
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Microsoft.Extensions.Configuration;
using Subsolo.AppHost;

var builder = DistributedApplication.CreateBuilder(args);

// Ambiente de publicação: docker-compose.yaml + .env (ADR-0004). Sem o dashboard do Aspire (imagem nightly): a
// telemetria vai para o Seq (ADR-0010).
builder.AddDockerComposeEnvironment("compose").WithDashboard(false);

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

// SPEC-0018 — no local e no CI, o e-mail transacional da API sai pelo Mailpit, sem TLS. Na publicação (Compose), a
// seção Notifications:Smtp fica para o go-live (SPEC-0003): provedor real, TLS e senha por segredo (ADR-0008).
if (!builder.ExecutionContext.IsPublishMode)
{
    var smtp = mailpit.GetEndpoint("smtp");
    api.WithEnvironment("Notifications__Smtp__Host", smtp.Property(EndpointProperty.Host))
        .WithEnvironment("Notifications__Smtp__Port", smtp.Property(EndpointProperty.Port))
        .WithEnvironment("Notifications__Smtp__Security", "None");
}

var web = builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/");

// SPEC-0009 — api e web exportam logs e traces para o Seq (ADR-0010); no local, os alertas de infra/seq/alerts.json
// são aplicados ao Seq e mandam e-mail pelo Mailpit.
api.WithTelemetryTo(seq);
web.WithTelemetryTo(seq);
if (!builder.ExecutionContext.IsPublishMode)
{
    seq.WithAlertsFrom(Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../infra/seq/alerts.json")), mailpit);
}

if (builder.ExecutionContext.IsPublishMode)
{
    // A API só sobe com o PostgreSQL aceitando conexões (no Compose, `depends_on` sozinho espera só o contêiner).
    postgres.PublishAsDockerComposeService((_, service) =>
    {
        service.Healthcheck = new Healthcheck
        {
            Test = ["CMD-SHELL", "pg_isready -U postgres"],
            Interval = "2s",
            Timeout = "5s",
            Retries = 30,
            StartPeriod = "5s",
        };

        // O banco da aplicação já nasce com o contêiner: a primeira migração não precisa criá-lo (sem o erro de
        // conexão que o EF registra ao checar um banco inexistente).
        service.Environment["POSTGRES_DB"] = database.Resource.DatabaseName;
    });
    api.PublishAsDockerComposeService((_, service) =>
    {
        service.DependsOn[postgres.Resource.Name] = new ServiceDependency { Condition = "service_healthy" };

        // Production efetivo por padrão (migrações pelo pipeline, ADR-0002), mesmo com o .env gerado vazio. O
        // Compose local (tools/compose-up.sh) define API_ENVIRONMENT=Development: migrações e seed no start.
        service.Environment["ASPNETCORE_ENVIRONMENT"] = "${API_ENVIRONMENT:-Production}";

        // Sem Kerberos: a imagem aspnet não tem a libgssapi_krb5, e o Npgsql tentaria a criptografia GSS (Prefer) a
        // cada conexão, registrando "cannot open shared object file" antes de seguir sem ela.
        service.Environment["PGGSSENCMODE"] = "disable";
    });

    // Caddy na frente (ADR-0004): /api/* e /health → api; o resto → web. Mesmo Caddyfile do deploy na VPS.
    // SPEC-0010: HTTPS em :8443 (certificado local `tls internal`, HSTS); o HTTP em :8080 redireciona com 308.
    // No Compose, o contêiner do web roda `next start` sobre o build de produção (Dockerfile gerado pelo Aspire).
#pragma warning disable ASPIREJAVASCRIPT001 // API em avaliação no Aspire 13.6; é o modo de publicação do Next.js sem `output: "standalone"`.
    web.PublishAsPackageScript("start");
#pragma warning restore ASPIREJAVASCRIPT001

    builder.AddContainer("caddy", "caddy", "2.11.7-alpine")
        .WithBindMount("../../infra/caddy/Caddyfile", "/etc/caddy/Caddyfile", isReadOnly: true)
        .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
        .WithHttpsEndpoint(port: 8443, targetPort: 8443, name: "https")
        .WithExternalHttpEndpoints()
        .WithEnvironment("API_UPSTREAM", api.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WithEnvironment("WEB_UPSTREAM", web.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WaitFor(api)
        .WaitFor(web);
}

await builder.Build().RunAsync();
