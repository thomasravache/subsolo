"use client";

import { useParams, usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { BottomNav } from "./BottomNav";
import type { CityOption } from "./CitySwitcher";
import { Footer } from "./Footer";
import { Header } from "./Header";
import { mainContentId } from "./shell";

export interface AppShellProps {
  cities: CityOption[];
  children: ReactNode;
}

/**
 * Troca de cidade: carrega o documento novo (navegação completa), não a navegação do router do cliente. Nesta, o
 * Next.js 16 entrega o `<title>` (metadados em streaming) num limite de `Suspense` à parte, que o React pode revelar
 * até ~300 ms depois do conteúdo — e nesse intervalo a página fica sem título (WCAG 2.4.2; o axe acusa
 * `document-title`). Com o documento novo, título e agenda chegam juntos.
 */
function goToCity(slug: string) {
  // eslint-disable-next-line @next/next/no-location-assign-relative-destination -- de propósito: documento novo, com o título (ver acima).
  window.location.assign(`/${encodeURIComponent(slug)}`);
}

/**
 * SPEC-0011 — shell das páginas públicas: link de pular, cabeçalho com o seletor de cidade, o conteúdo (cada página
 * traz o seu `<main id="conteudo">`), rodapé e navegação inferior no celular. A cidade atual vem da URL
 * (`/{cidade}`); escolher outra no seletor navega para `/{outra}`.
 */
export function AppShell({ cities, children }: AppShellProps) {
  const params = useParams<{ cidade?: string }>();
  const pathname = usePathname();
  const city = typeof params?.cidade === "string" ? params.cidade : "";
  const current = pathname === "/apoie" ? "apoie" : city ? "agenda" : undefined;

  return (
    <div className="flex flex-1 flex-col pb-16 md:pb-0">
      <a
        href={`#${mainContentId}`}
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-2 focus:z-[60] focus:rounded-md focus:bg-surface-raised focus:px-4 focus:py-3 focus:text-text"
      >
        Pular para o conteúdo
      </a>
      <Header city={city} cities={cities} onCityChange={goToCity} />
      <div className="flex flex-1 flex-col">{children}</div>
      <Footer />
      <BottomNav city={city} current={current} />
    </div>
  );
}
