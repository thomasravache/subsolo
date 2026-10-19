import Link from "next/link";

/** Caminho do cadastro de show (anônimo: a página manda para `/entrar?returnTo=/conta/shows/novo`). */
export const newShowHref = "/conta/shows/novo";

/**
 * SPEC-0025 — bloco no fim da agenda (Stitch "Agenda Curitiba"): "PRODUZINDO SHOW EM {CIDADE}?" e o link "Cadastrar
 * evento na agenda", que leva ao cadastro de show (quem não entrou passa antes por `/entrar`, com a volta para lá). O
 * texto do link fica na cor do texto, sublinhado em vermelho (o vermelho em texto pequeno não tem contraste 4,5:1).
 */
export function AgendaProducerCta({ cityName }: { cityName: string }) {
  return (
    <section
      aria-labelledby="agenda-produtor"
      className="flex flex-col items-center gap-2 rounded-md border border-dashed border-border p-4 text-center"
    >
      <h2
        id="agenda-produtor"
        className="font-label text-xs font-bold uppercase tracking-widest text-text-muted"
      >
        Produzindo show em {cityName}?
      </h2>
      <Link
        href={newShowHref}
        className="inline-flex min-h-11 items-center gap-1.5 font-label text-xs font-bold uppercase tracking-wider text-text underline decoration-accent decoration-2 underline-offset-4 hover:decoration-text"
      >
        Cadastrar evento na agenda
        <svg
          viewBox="0 0 24 24"
          width="16"
          height="16"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="square"
          aria-hidden="true"
          focusable="false"
          className="text-accent"
        >
          <path d="M4 12h15M13 6l6 6-6 6" />
        </svg>
      </Link>
    </section>
  );
}
