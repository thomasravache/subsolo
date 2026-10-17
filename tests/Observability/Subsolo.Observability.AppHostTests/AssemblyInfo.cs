// SPEC-0009 Emenda 3: o Next 16 trava `web/.next/dev/lock`, então só um AppHost com `next dev` pode rodar por vez.
// As classes deste assembly sobem o AppHost e rodam em série. No xUnit v3 4.x, `ParallelMode.None` substitui o
// obsoleto `[assembly: CollectionBehavior(DisableTestParallelization = true)]`.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
