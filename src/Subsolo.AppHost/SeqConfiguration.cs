// SPEC-0009 — aplica infra/seq/alerts.json (ADR-0010) ao Seq local pela API HTTP do Seq: retenção, o app de e-mail
// (Seq.App.EmailPlus, do feed nuget.org do próprio Seq), um sinal salvo e um alerta por item, cada alerta com uma
// instância do app de e-mail que manda o link do runbook. Idempotente por título: o que já existe não é recriado.
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Subsolo.AppHost;

internal static partial class SeqConfiguration
{
    /// <summary>
    /// O serviço exporta logs e traces por OTLP (<c>http/protobuf</c>) para o Seq, com o nome do recurso como
    /// <c>service.name</c>. Variáveis padrão do OpenTelemetry: valem para a API (.NET) e para o web (Node.js), no
    /// <c>aspire run</c> e no Compose publicado.
    /// </summary>
    public static IResourceBuilder<T> WithTelemetryTo<T>(this IResourceBuilder<T> service, IResourceBuilder<SeqResource> seq)
        where T : IResourceWithEnvironment =>
        service
            .WithEnvironment("OTEL_EXPORTER_OTLP_ENDPOINT", ReferenceExpression.Create($"{seq.GetEndpoint("http")}/ingest/otlp"))
            .WithEnvironment("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf")
            .WithEnvironment("OTEL_SERVICE_NAME", service.Resource.Name);

    /// <summary>
    /// Quando o Seq local fica pronto (<c>aspire run</c> e testes do AppHost), aplica <paramref name="alertsFile"/>
    /// com o e-mail saindo pelo endpoint <c>smtp</c> de <paramref name="mailpit"/> (alcançado pela rede dos
    /// contêineres). Uma falha (ex.: sem acesso ao nuget.org para instalar o app de e-mail) só gera aviso no log do Seq.
    /// </summary>
    public static IResourceBuilder<SeqResource> WithAlertsFrom(
        this IResourceBuilder<SeqResource> seq, string alertsFile, IResourceBuilder<ContainerResource> mailpit) =>
        seq.OnResourceReady(async (resource, ready, cancellationToken) =>
        {
            var logger = ready.Services.GetRequiredService<ResourceLoggerService>().GetLogger(resource);
            var smtp = mailpit.GetEndpoint("smtp");
            try
            {
                await ApplyAsync(
                    new Uri(seq.GetEndpoint("http").Url), alertsFile, mailpit.Resource.Name,
                    smtp.TargetPort ?? 1025, logger, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or IOException or JsonException)
            {
                LogApplyFailed(logger, exception, alertsFile);
            }
        });

    private const string NuGetFeedId = "nugetfeed-1";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Aplica <paramref name="configPath"/> ao Seq em <paramref name="seqUrl"/>; o Seq alcança o servidor SMTP em
    /// <paramref name="smtpHost"/>:<paramref name="smtpPort"/>.
    /// </summary>
    public static async Task ApplyAsync(
        Uri seqUrl, string configPath, string smtpHost, int smtpPort, ILogger logger, CancellationToken cancellationToken)
    {
        var config = JsonSerializer.Deserialize<SeqAlertsFile>(await File.ReadAllTextAsync(configPath, cancellationToken), Options)
                     ?? throw new InvalidOperationException($"{configPath} vazio");
        using var seq = new HttpClient { BaseAddress = seqUrl, Timeout = TimeSpan.FromMinutes(2) };

        await EnsureRetentionAsync(seq, config.RetentionDays, cancellationToken);
        var appId = await EnsureEmailAppAsync(seq, config.Email.App, cancellationToken);

        foreach (var alert in config.Alerts)
        {
            var instanceId = await EnsureAsync(seq, "appinstances", $"Alerta: {alert.Title}", async () =>
            {
                var instance = await TemplateAsync(seq, $"api/appinstances/template?appId={Uri.EscapeDataString(appId)}", cancellationToken);
                instance["Title"] = $"Alerta: {alert.Title}";
                var settings = instance["Settings"]!.AsObject();
                settings["From"] = config.Email.From;
                settings["To"] = config.Email.To;
                settings["Host"] = smtpHost;
                settings["Port"] = smtpPort.ToString(CultureInfo.InvariantCulture);
                settings["SubjectTemplate"] = $"[Subsolo] Alerta: {alert.Title}";
                settings["BodyTemplate"] =
                    $"<p>Alerta <strong>{alert.Title}</strong> disparado: {{{{$Message}}}}</p>" +
                    $"<p>Runbook: <a href=\"{alert.Runbook}\">{alert.Runbook}</a></p>";
                return instance;
            }, cancellationToken);

            var signalId = await EnsureAsync(seq, "signals", alert.Title, async () =>
            {
                var signal = await TemplateAsync(seq, "api/signals/template", cancellationToken);
                signal["Title"] = alert.Title;
                signal["Description"] = $"SPEC-0009 — runbook: {alert.Runbook}";
                signal["OwnerId"] = null;
                signal["Filters"] = new JsonArray(new JsonObject { ["Filter"] = alert.Filter });
                return signal;
            }, cancellationToken);

            await EnsureAsync(seq, "alerts", alert.Title, async () =>
            {
                var created = await TemplateAsync(seq, $"api/alerts/template?signal={Uri.EscapeDataString(signalId)}", cancellationToken);
                created["Title"] = alert.Title;
                created["Description"] = $"Runbook: {alert.Runbook}";
                created["OwnerId"] = null;
                created["TimeGrouping"] = alert.Window;
                created["Having"] = alert.Having;
                created["NotificationChannels"] = new JsonArray(new JsonObject { ["NotificationAppInstanceId"] = instanceId });
                return created;
            }, cancellationToken);

            LogAlertApplied(logger, alert.Title, alert.Runbook);
        }
    }

    private static async Task EnsureRetentionAsync(HttpClient seq, int days, CancellationToken cancellationToken)
    {
        var retention = TimeSpan.FromDays(days).ToString("c", CultureInfo.InvariantCulture);
        var existing = await seq.GetFromJsonAsync<JsonArray>("api/retentionpolicies", cancellationToken) ?? [];
        if (existing.Any(policy => (string?)policy?["RetentionTime"] == retention && policy?["RemovedSignalExpression"] is null))
        {
            return;
        }

        var policy = await TemplateAsync(seq, "api/retentionpolicies/template", cancellationToken);
        policy["RetentionTime"] = retention;
        policy["RemovedSignalExpression"] = null;
        await CreateAsync(seq, "retentionpolicies", policy, cancellationToken);
    }

    private static async Task<string> EnsureEmailAppAsync(HttpClient seq, SeqAppPackage package, CancellationToken cancellationToken)
    {
        var apps = await seq.GetFromJsonAsync<JsonArray>("api/apps", cancellationToken) ?? [];
        var installed = apps.FirstOrDefault(app => (string?)app?["Package"]?["PackageId"] == package.PackageId);
        if (installed?["Id"] is { } id)
        {
            return (string)id!;
        }

        using var response = await seq.PostAsync(
            new Uri(
                $"api/apps/install?feedId={NuGetFeedId}&packageId={Uri.EscapeDataString(package.PackageId)}" +
                $"&version={Uri.EscapeDataString(package.Version)}",
                UriKind.Relative),
            content: null,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var app = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return (string?)app?["Id"] ?? throw new InvalidOperationException($"o Seq não devolveu o Id do app {package.PackageId}");
    }

    /// <summary>Id do item de <paramref name="collection"/> com o título <paramref name="title"/>, criado se não existir.</summary>
    private static async Task<string> EnsureAsync(
        HttpClient seq, string collection, string title, Func<Task<JsonObject>> create, CancellationToken cancellationToken)
    {
        var existing = await seq.GetFromJsonAsync<JsonArray>($"api/{collection}?shared=true", cancellationToken) ?? [];
        var found = existing.FirstOrDefault(item => (string?)item?["Title"] == title);
        if (found?["Id"] is { } id)
        {
            return (string)id!;
        }

        return await CreateAsync(seq, collection, await create(), cancellationToken);
    }

    private static async Task<JsonObject> TemplateAsync(HttpClient seq, string path, CancellationToken cancellationToken) =>
        await seq.GetFromJsonAsync<JsonObject>(path, cancellationToken)
        ?? throw new InvalidOperationException($"o Seq não devolveu o modelo {path}");

    private static async Task<string> CreateAsync(HttpClient seq, string collection, JsonObject item, CancellationToken cancellationToken)
    {
        item.Remove("Links");
        using var response = await seq.PostAsJsonAsync($"api/{collection}/", item, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"o Seq recusou {collection} ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var created = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        return (string?)created?["Id"] ?? throw new InvalidOperationException($"o Seq não devolveu o Id em {collection}");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seq: não foi possível aplicar {AlertsFile}; alertas inativos")]
    private static partial void LogApplyFailed(ILogger logger, Exception exception, string alertsFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seq: alerta \"{Alert}\" aplicado (runbook {Runbook})")]
    private static partial void LogAlertApplied(ILogger logger, string alert, string runbook);

    private sealed record SeqAlertsFile(
        [property: JsonPropertyName("retentionDays")] int RetentionDays,
        [property: JsonPropertyName("email")] SeqEmail Email,
        [property: JsonPropertyName("alerts")] IReadOnlyList<SeqAlert> Alerts);

    private sealed record SeqEmail(
        [property: JsonPropertyName("app")] SeqAppPackage App,
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string To);

    private sealed record SeqAppPackage(
        [property: JsonPropertyName("packageId")] string PackageId,
        [property: JsonPropertyName("version")] string Version);

    private sealed record SeqAlert(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("filter")] string Filter,
        [property: JsonPropertyName("window")] string Window,
        [property: JsonPropertyName("having")] string Having,
        [property: JsonPropertyName("runbook")] string Runbook);
}
