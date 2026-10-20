import type { SupportersResult } from "@/api/supporters";

import { supportMessages } from "./messages";

/**
 * SPEC-0028 — a seção "APOIADORES" da página Apoie: os nomes na ordem de cadastro (com link `https` quando houver, em
 * nova aba); vazia → "Seja o primeiro a apoiar."; API fora do ar → "A lista de apoiadores volta já.".
 */
export function SupportersList({ result }: { result: SupportersResult }) {
  return (
    <section aria-labelledby="apoiadores" className="flex flex-col gap-4">
      <h2
        id="apoiadores"
        className="font-display text-3xl uppercase tracking-wide text-text"
      >
        Apoiadores
      </h2>
      {result.kind === "unavailable" ? (
        <p className="text-base text-text-muted">
          {supportMessages.supportersUnavailable}
        </p>
      ) : result.supporters.length === 0 ? (
        <p className="text-base text-text-muted">
          {supportMessages.supportersEmpty}
        </p>
      ) : (
        <ul className="flex flex-col gap-2">
          {result.supporters.map((supporter) => (
            <li
              key={supporter.id}
              className="rounded-md border border-border bg-surface px-4 py-3"
            >
              {supporter.url ? (
                <a
                  href={supporter.url}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex min-h-11 items-center font-label text-base font-bold tracking-wide text-text underline decoration-accent decoration-2 underline-offset-4 hover:text-white"
                >
                  {supporter.name}
                  <span className="sr-only"> (abre em nova aba)</span>
                </a>
              ) : (
                <span className="inline-flex min-h-11 items-center font-label text-base font-bold tracking-wide text-text">
                  {supporter.name}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
