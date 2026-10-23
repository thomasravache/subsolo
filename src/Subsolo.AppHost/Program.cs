// SPEC-0007 — modelo da aplicação no Aspire (ADR-0004): `aspire run` sobe PostgreSQL 18, Seq, Mailpit, a API e o front.
// SPEC-0008 — `aspire publish -o artifacts/compose` gera o Docker Compose da mesma aplicação, com o Caddy na frente.
// SPEC-0022 — mídia (ADR-0015): no `aspire run`, Media__Root é um diretório local (artifacts/media) servido pela API; no
// Compose, o volume `media`, montado na API e no Caddy (que serve /media/*).
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
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

// SPEC-0018 — o e-mail transacional da API sai pelo Mailpit: Host e Port do endpoint smtp nos dois modos. Em `aspire run`
// (local e CI), sem TLS; no Compose publicado, Security vem de SMTP_SECURITY com padrão StartTls (a API fora de
// Development exige TLS) — a pilha local usa None. Provedor real, TLS e senha por segredo no go-live (SPEC-0003).
var smtp = mailpit.GetEndpoint("smtp");
api.WithEnvironment("Notifications__Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Notifications__Smtp__Port", smtp.Property(EndpointProperty.Port));
if (!builder.ExecutionContext.IsPublishMode)
{
    api.WithEnvironment("Notifications__Smtp__Security", "None");
}

// SPEC-0022 — onde o volume `media` fica montado no Compose (na API e no Caddy, mesmo caminho) e o usuário da imagem
// da API (o padrão das imagens .NET, APP_UID), dono do volume para poder gravar os flyers.
const string MediaVolume = "media";
const string MediaMount = "/srv/media";
const string ApiContainerUser = "1654";
if (!builder.ExecutionContext.IsPublishMode)
{
    api.WithEnvironment("Media__Root", Path.GetFullPath(Path.Combine(builder.AppHostDirectory, "../../artifacts/media")));
}

var web = builder.AddJavaScriptApp("web", "../../web", "dev")
    .WithPnpm(installArgs: ["--frozen-lockfile"])
    .WithHttpEndpoint(env: "PORT")
    .WithEnvironment("API_URL", api.GetEndpoint("http"))
    .WithReference(api)
    .WaitFor(api)
    .WithHttpHealthCheck("/");

// SPEC-0016 (Emenda 4, §7.6 j) — no `aspire run`, o link mágico do e-mail aponta para o web (a API valida
// App:PublicUrl na inicialização). No Compose publicado, o operador define App__PublicUrl (a pilha local, pelo
// tools/compose-up.sh). SPEC-0024 (Emenda v2, a) — o web recebe o mesmo endereço em SUBSOLO_PUBLIC_URL (só do servidor,
// sem NEXT_PUBLIC_: nunca chega ao navegador), para as URLs absolutas do Open Graph e o canonical da página do show.
if (!builder.ExecutionContext.IsPublishMode)
{
    api.WithEnvironment("App__PublicUrl", web.GetEndpoint("http"));
    web.WithEnvironment("SUBSOLO_PUBLIC_URL", web.GetEndpoint("http"));
}

// SPEC-0020 — no `aspire run`, o provedor OpenID Connect falso (mock-oauth2-server, login interativo) faz o papel do
// Google. Porta fixa no host e sem proxy do Aspire (`Subsolo:FakeGooglePort`, padrão 8090): o emissor
// `http://127.0.0.1:<porta>/default` é o mesmo para o navegador e para a API (que roda no host). O segredo do cliente é
// um parâmetro secreto gerado a cada execução (o provedor falso aceita qualquer um). No Compose publicado não há provedor
// falso: o operador configura Users__Google__* com o cliente real do Google no go-live (SPEC-0003); sem isso, desligado.
if (!builder.ExecutionContext.IsPublishMode)
{
    var fakeGooglePort = builder.Configuration.GetValue("Subsolo:FakeGooglePort", 8090);
    var googleClientSecret = builder.AddParameter(
        "google-client-secret",
        new GenerateParameterDefault { MinLength = 32, Special = false },
        secret: true);
    builder.AddContainer("google", "ghcr.io/navikt/mock-oauth2-server", "6.0.4")
        .WithHttpEndpoint(port: fakeGooglePort, targetPort: 8080, name: "http", isProxied: false)
        .WithEnvironment("SERVER_PORT", "8080")
        .WithEnvironment("JAVA_TOOL_OPTIONS", "-Xmx128m")
        .WithEnvironment("JSON_CONFIG", """{"interactiveLogin":true,"httpServer":"NettyWrapper"}""")
        .WithHttpHealthCheck("/isalive");
    var fakeGoogleIssuer = string.Create(CultureInfo.InvariantCulture, $"http://127.0.0.1:{fakeGooglePort}/default");
    api.WithEnvironment("Users__Google__Authority", fakeGoogleIssuer)
        .WithEnvironment("Users__Google__ClientId", "subsolo-local")
        .WithEnvironment("Users__Google__ClientSecret", googleClientSecret);
}

// SPEC-0030 — no `aspire run`, a API recebe um par de chaves VAPID gerado a cada execução (as inscrições de push de
// desenvolvimento valem até a próxima subida) e uma chave aleatória para o token de descadastro dos e-mails de aviso,
// os dois como parâmetros secretos: nenhuma chave fica no repositório (ADR-0008). As de produção entram no go-live
// (SPEC-0003). H28-05 (herança da revisão da SPEC-0028) — o web recebe a chave Pix, o recebedor e o e-mail de contato
// de desenvolvimento, como o tools/compose-up.sh faz na pilha do Compose; só do servidor (sem NEXT_PUBLIC_).
if (!builder.ExecutionContext.IsPublishMode)
{
    using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var vapidKey = vapid.ExportParameters(includePrivateParameters: true);
    var vapidPublicKey = Base64Url.EncodeToString([0x04, .. vapidKey.Q.X!, .. vapidKey.Q.Y!]);
    var vapidPrivateKey = builder.AddParameter("vapid-private-key", Base64Url.EncodeToString(vapidKey.D), secret: true);
    var unsubscribeKey = builder.AddParameter(
        "unsubscribe-signing-key",
        new GenerateParameterDefault { MinLength = 32, Special = false },
        secret: true);
    api.WithEnvironment("Notifications__WebPush__PublicKey", vapidPublicKey)
        .WithEnvironment("Notifications__WebPush__PrivateKey", vapidPrivateKey)
        .WithEnvironment("Notifications__WebPush__Subject", "mailto:nao-responda@subsolo.local")
        .WithEnvironment("Notifications__Unsubscribe__SigningKey", unsubscribeKey);
    web.WithEnvironment("Support__PixKey", "chave-dev@subsolo.local")
        .WithEnvironment("Support__PixName", "Subsolo")
        .WithEnvironment("Support__PixCity", "Curitiba")
        .WithEnvironment("Site__ContactEmail", "contato-dev@subsolo.local");
}

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

        // SPEC-0018 (Emenda 2): TLS no SMTP por padrão; o operador (ou a pilha local, com o Mailpit) define SMTP_SECURITY.
        service.Environment["Notifications__Smtp__Security"] = "${SMTP_SECURITY:-StartTls}";

        // Sem Kerberos: a imagem aspnet não tem a libgssapi_krb5, e o Npgsql tentaria a criptografia GSS (Prefer) a
        // cada conexão, registrando "cannot open shared object file" antes de seguir sem ela.
        service.Environment["PGGSSENCMODE"] = "disable";

        // SPEC-0022: os flyers vão para o volume `media` (o Caddy serve do mesmo volume).
        service.Environment["Media__Root"] = MediaMount;
        service.Volumes.Add(new Volume { Name = MediaVolume, Source = MediaVolume, Target = MediaMount, Type = "volume" });
    });

    // SPEC-0022: um volume novo nasce do root, e a API roda sem privilégios (APP_UID). Antes da API, um passo único
    // (com a imagem do Caddy, que já está na pilha) passa o volume para o usuário dela.
    var mediaInit = builder.AddContainer("media-init", "caddy", "2.11.7-alpine")
        .WithVolume(MediaVolume, MediaMount)
        .WithEntrypoint("/bin/sh")
        .WithArgs("-c", $"chown {ApiContainerUser}:{ApiContainerUser} {MediaMount}");
    api.WaitForCompletion(mediaInit);

    // Caddy na frente (ADR-0004): /api/* e /health → api; /media/* → volume media (SPEC-0022); o resto → web. Mesmo
    // Caddyfile do deploy na VPS.
    // SPEC-0010: HTTPS em :8443 (certificado local `tls internal`, HSTS); o HTTP em :8080 redireciona com 308.
    // No Compose, o contêiner do web roda `next start` sobre o build de produção (Dockerfile gerado pelo Aspire).
#pragma warning disable ASPIREJAVASCRIPT001 // API em avaliação no Aspire 13.6; é o modo de publicação do Next.js sem `output: "standalone"`.
    web.PublishAsPackageScript("start");
#pragma warning restore ASPIREJAVASCRIPT001

    builder.AddContainer("caddy", "caddy", "2.11.7-alpine")
        .WithBindMount("../../infra/caddy/Caddyfile", "/etc/caddy/Caddyfile", isReadOnly: true)
        .WithVolume(MediaVolume, MediaMount, isReadOnly: true)
        .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
        .WithHttpsEndpoint(port: 8443, targetPort: 8443, name: "https")
        .WithExternalHttpEndpoints()
        .WithEnvironment("API_UPSTREAM", api.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WithEnvironment("WEB_UPSTREAM", web.GetEndpoint("http").Property(EndpointProperty.HostAndPort))
        .WaitFor(api)
        .WaitFor(web);
}

await builder.Build().RunAsync();
