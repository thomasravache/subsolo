namespace Subsolo.AppHost.IntegrationTests;

/// <summary>
/// SPEC-0040 (Emenda 1) — testes que sobem o <c>Subsolo.AppHost</c> em modo run: rodam um de cada vez, sem
/// paralelismo. O Caddy (8443/8080) e o Google falso (8090) usam portas fixas no host, e só cabe um <c>next dev</c> por
/// pasta do web.
/// </summary>
[CollectionDefinition(nameof(AppHostRunTests), DisableParallelization = true)]
public sealed class AppHostRunTests;
