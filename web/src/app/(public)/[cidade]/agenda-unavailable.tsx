/** SPEC-0008 — mensagem da agenda quando a API está fora do ar (status 503 pelo `proxy`, ou a página de erro). */
export function AgendaUnavailable() {
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-4 px-4 py-10">
      <h1 className="text-3xl font-semibold tracking-tight">
        Agenda indisponível
      </h1>
      <p role="alert" className="text-lg text-foreground/80">
        A agenda está fora do ar por alguns instantes. Tente de novo.
      </p>
    </main>
  );
}
