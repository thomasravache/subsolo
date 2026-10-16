"use client";

import { useParams, usePathname, useRouter } from "next/navigation";
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
 * SPEC-0011 — shell das páginas públicas: link de pular, cabeçalho com o seletor de cidade, o conteúdo (cada página
 * traz o seu `<main id="conteudo">`), rodapé e navegação inferior no celular. A cidade atual vem da URL
 * (`/{cidade}`); escolher outra no seletor navega para `/{outra}`.
 */
export function AppShell({ cities, children }: AppShellProps) {
  const params = useParams<{ cidade?: string }>();
  const pathname = usePathname();
  const router = useRouter();
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
      <Header
        city={city}
        cities={cities}
        onCityChange={(slug) => router.push(`/${slug}`)}
      />
      <div className="flex flex-1 flex-col">{children}</div>
      <Footer />
      <BottomNav city={city} current={current} />
    </div>
  );
}
