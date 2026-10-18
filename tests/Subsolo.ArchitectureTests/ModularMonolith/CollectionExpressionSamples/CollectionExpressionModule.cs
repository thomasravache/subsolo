namespace Subsolo.Modules.CollectionSampleA.Application
{
    /// <summary>
    /// SPEC-0021 IT-08 (§7.6, herança da revisão da SPEC-0017) — dublê: um "módulo" que usa expressões de coleção (o
    /// compilador gera tipos sem namespace, como <c>&lt;&gt;z__ReadOnlyArray&lt;T&gt;</c>, que o ArchUnitNET pode não
    /// resolver) e depende só do IntegrationEvents de outro módulo de exemplo.
    /// </summary>
    public static class CollectionExpressionUser
    {
        public static IReadOnlyList<CollectionSampleB.IntegrationEvents.SampleHappened> One(Guid id) =>
            [new CollectionSampleB.IntegrationEvents.SampleHappened(id)];

        public static IReadOnlyList<string> Names(string first, string second) => [first, second];

        public static IEnumerable<int> Numbers() => [1, 2, 3];

        public static IReadOnlyCollection<Uri> Uris(Uri first) => [first, .. Array.Empty<Uri>()];
    }
}

namespace Subsolo.Modules.CollectionSampleB.IntegrationEvents
{
    /// <summary>SPEC-0021 IT-08 — dublê: o contrato público do segundo módulo de exemplo.</summary>
    public sealed record SampleHappened(Guid Id);
}
