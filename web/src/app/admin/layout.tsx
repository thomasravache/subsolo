import Link from "next/link";
import type { ReactNode } from "react";

import { mainContentId } from "@/components/ui/shell";

/**
 * SPEC-0017 — moldura do painel do admin (desktop primeiro, utilizável no celular): link de pular, cabeçalho com o
 * wordmark (volta ao site) e o selo "Admin". Sem o seletor de cidade nem a navegação do site público; o status de cada
 * página (307/403/503) é decidido pelo `proxy` (ADR-0016).
 */
export const instant = false;

export default function AdminLayout({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-1 flex-col">
      <a
        href={`#${mainContentId}`}
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-2 focus:z-[60] focus:rounded-md focus:bg-surface-raised focus:px-4 focus:py-3 focus:text-text"
      >
        Pular para o conteúdo
      </a>
      <header className="border-b border-border bg-bg">
        <div className="mx-auto flex h-14 w-full max-w-7xl items-center gap-3 px-4">
          <Link
            href="/"
            className="inline-flex min-h-11 items-center font-display text-2xl uppercase tracking-widest text-text hover:text-white"
          >
            Subsolo
          </Link>
          <span className="rounded-xs border border-border px-1.5 py-0.5 font-label text-xs font-bold uppercase tracking-wider text-text-muted">
            Admin
          </span>
        </div>
      </header>
      <div className="flex flex-1 flex-col">{children}</div>
    </div>
  );
}
