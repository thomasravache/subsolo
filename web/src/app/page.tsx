/**
 * Página inicial. SPEC-0019 — depois de excluir a conta, o fã chega em `/?conta=excluida` e vê o aviso "Sua conta foi
 * excluída." (`role="status"`); o resto da página não muda.
 */
export default async function Home({ searchParams }: PageProps<"/">) {
  const { conta } = await searchParams;
  return (
    <main className="flex flex-1 flex-col items-center justify-center gap-4 px-6 py-24 text-center">
      {conta === "excluida" && (
        <p
          role="status"
          className="rounded-md border border-border bg-surface px-4 py-3 text-base text-text"
        >
          Sua conta foi excluída.
        </p>
      )}
      <h1 className="text-4xl font-semibold tracking-tight">Subsolo</h1>
      <p className="max-w-md text-lg text-foreground/70">
        Agenda de shows de metal underground por cidade. Em construção.
      </p>
    </main>
  );
}
