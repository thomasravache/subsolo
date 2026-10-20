import type { ReactNode } from "react";

import { mainContentId } from "@/components/ui/shell";

export interface InstitutionalSection {
  /** Âncora da seção (`#id`), usada pelo índice. */
  id: string;
  title: string;
  content: ReactNode;
}

export interface InstitutionalPageProps {
  title: string;
  /** Data da última atualização do texto, por extenso ("19 de outubro de 2026"). */
  updatedAt: string;
  intro: ReactNode;
  sections: InstitutionalSection[];
}

/** Número da seção com 2 dígitos ("01"), decorativo: o nome da seção é o que o leitor de tela lê. */
function number(index: number): string {
  return String(index + 1).padStart(2, "0");
}

/**
 * SPEC-0028 — moldura das páginas institucionais (tela "Privacidade - Subsolo" do Stitch, também modelo de `/termos`):
 * título, "Atualizada em …", introdução, o índice ("Neste documento", um `<nav>` com âncoras para cada seção) e as
 * seções numeradas. Texto estático, versionado no repositório.
 */
export function InstitutionalPage({
  title,
  updatedAt,
  intro,
  sections,
}: InstitutionalPageProps) {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-8 px-4 py-10"
    >
      <div className="flex flex-col gap-3">
        <h1 className="font-display text-5xl uppercase leading-none tracking-wide text-text">
          {title}
        </h1>
        <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
          Atualizada em {updatedAt}
        </p>
      </div>

      <div className="flex flex-col gap-4 border-l-2 border-accent pl-4 text-base text-text">
        {intro}
      </div>

      <nav
        aria-labelledby="neste-documento"
        className="rounded-md border border-border bg-surface p-4"
      >
        <h2
          id="neste-documento"
          className="mb-2 font-label text-xs font-bold uppercase tracking-wider text-text-muted"
        >
          Neste documento
        </h2>
        <ol className="flex flex-col">
          {sections.map((section, index) => (
            <li key={section.id}>
              <a
                href={`#${section.id}`}
                className="flex min-h-11 items-center gap-3 font-label text-sm font-bold uppercase tracking-wider text-text hover:text-white"
              >
                <span aria-hidden="true" className="text-text-muted">
                  {number(index)}
                </span>
                <span>{section.title}</span>
              </a>
            </li>
          ))}
        </ol>
      </nav>

      <div className="flex flex-col gap-10">
        {sections.map((section, index) => (
          <section
            key={section.id}
            id={section.id}
            aria-labelledby={`${section.id}-titulo`}
            className="flex scroll-mt-20 flex-col gap-4"
          >
            <div className="flex items-baseline gap-3 border-b border-border pb-2">
              <span
                aria-hidden="true"
                className="font-label text-sm font-bold text-text-muted"
              >
                {number(index)}
              </span>
              <h2
                id={`${section.id}-titulo`}
                className="font-display text-3xl uppercase tracking-wide text-text"
              >
                {section.title}
              </h2>
            </div>
            <div className="flex flex-col gap-3 text-base leading-relaxed text-text-muted [&_strong]:text-text [&_ul]:flex [&_ul]:list-disc [&_ul]:flex-col [&_ul]:gap-2 [&_ul]:pl-5">
              {section.content}
            </div>
          </section>
        ))}
      </div>
    </main>
  );
}

/** O e-mail de contato como link `mailto:`, ou o aviso de que o canal é publicado antes do lançamento. */
export function ContactEmail({ email }: { email: string | undefined }) {
  return email ? (
    <a
      href={`mailto:${email}`}
      className="font-bold text-text underline decoration-accent decoration-2 underline-offset-4 hover:text-white"
    >
      {email}
    </a>
  ) : (
    <span>o e-mail de contato publicado aqui antes do lançamento</span>
  );
}
