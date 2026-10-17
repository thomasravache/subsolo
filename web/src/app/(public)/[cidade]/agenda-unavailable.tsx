import { mainContentId } from "@/components/ui/shell";

/** SPEC-0008 — mensagem da agenda quando a API está fora do ar (status 503 pelo `proxy`, ou a página de erro). */
export function AgendaUnavailable() {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-4 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Agenda indisponível
      </h1>
      <p role="alert" className="text-lg text-text-muted">
        A agenda está fora do ar por alguns instantes. Tente de novo.
      </p>
    </main>
  );
}
