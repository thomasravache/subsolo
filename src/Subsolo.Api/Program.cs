// SPEC-0007 — composição do host da API (ADR-0001): infraestrutura comum, módulos e endpoints.
using System.Text.Json.Serialization;
using Subsolo.Common.Infrastructure;
using Subsolo.Common.Infrastructure.Database;
using Subsolo.Common.Presentation;
using Subsolo.Modules.Events.Infrastructure;
using Subsolo.Modules.Events.Infrastructure.Cities;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// Números só como números no JSON: o contrato (e o cliente TypeScript gerado) não aceita inteiros em string.
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict);
builder.Services.AddCommonInfrastructure(builder.Configuration);

// Módulos: services.Add<M>Module(builder.Configuration) — cada um chama AddModule e AddEndpoints.
builder.Services.AddEventsModule(builder.Configuration);

var app = builder.Build();

// Exceção não tratada → 500 em ProblemDetails (RFC 9457); fora de Development, sem detalhes da exceção.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Só em desenvolvimento: nos outros ambientes, o pipeline aplica o bundle de migrações (ADR-0002).
    await app.Services.MigrateModulesAsync();
    await app.Services.SeedEventsDevelopmentDataAsync();
}

app.MapDefaultEndpoints();
app.MapEndpoints();

await app.RunAsync();

/// <summary>Ponto de entrada da API; público para o <c>WebApplicationFactory</c> dos testes.</summary>
public partial class Program;
