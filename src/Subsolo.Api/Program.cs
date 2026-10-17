// SPEC-0007 — composição do host da API (ADR-0001): infraestrutura comum, módulos e endpoints.
// SPEC-0010 — segurança base (ADR-0003): autorização negada por padrão, anti-forgery e rate limiting.
using System.Text.Json.Serialization;
using OpenTelemetry.Metrics;
using Subsolo.Api.OpenApi;
using Subsolo.Api.Security;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Presentation;
using Subsolo.Common.Presentation.Security;
using Subsolo.Modules.Events.Infrastructure;
using Subsolo.Modules.Events.Infrastructure.Cities;
using Subsolo.Modules.Notifications.Infrastructure;
using Subsolo.Modules.Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options => options
    .AddSchemaTransformer<ProblemDetailsCodeSchemaTransformer>()
    .AddOperationTransformer<ProblemCodeOperationTransformer>());

// Números só como números no JSON: o contrato (e o cliente TypeScript gerado) não aceita inteiros em string.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddCommonInfrastructure(builder.Configuration);
builder.Services.AddApiSecurity(builder.Configuration);

// Módulos: services.Add<M>Module(builder.Configuration) — cada um chama AddModule e AddEndpoints.
builder.Services.AddEventsModule(builder.Configuration);
builder.Services.AddUsersModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

// SPEC-0014: métricas dos módulos (subsolo.users.sign_ins e subsolo.users.sessions_revoked).
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(UsersModule.MeterName));

var app = builder.Build();

// Exceção não tratada → 500 em ProblemDetails (RFC 9457); fora de Development, sem detalhes da exceção.
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseApiSecurity();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    // Só em desenvolvimento: nos outros ambientes, o pipeline aplica o bundle de migrações (ADR-0002).
    await app.Services.MigrateModulesAsync();
    await app.Services.SeedEventsDevelopmentDataAsync();
}

app.MapDefaultEndpoints();
app.MapAntiforgeryEndpoint();

// Endpoints dos módulos: toda mutação passa pelo anti-forgery (cabeçalho X-CSRF).
app.MapEndpoints(app.MapGroup(string.Empty).AddEndpointFilter<AntiforgeryFilter>());

await app.RunAsync();

/// <summary>Ponto de entrada da API; público para o <c>WebApplicationFactory</c> dos testes.</summary>
public partial class Program;
