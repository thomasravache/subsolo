import Link from "next/link";

import { CitySwitcher, type CityOption } from "./CitySwitcher";

export interface HeaderProps {
  /** Slug da cidade atual (vazio fora das páginas de cidade). */
  city: string;
  cities: CityOption[];
  onCityChange: (slug: string) => void;
}

const navLink =
  "inline-flex min-h-11 items-center px-2 font-label text-xs font-bold uppercase tracking-wider text-text-muted hover:text-text";

/**
 * SPEC-0011 — cabeçalho do app: wordmark "Subsolo", navegação (só no desktop; no celular fica a navegação inferior)
 * e o seletor de cidade (quando há cidades para escolher).
 */
export function Header({ city, cities, onCityChange }: HeaderProps) {
  return (
    <header className="sticky top-0 z-40 border-b border-border bg-bg">
      <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between gap-4 px-4">
        <Link
          href={city ? `/${city}` : "/"}
          className="inline-flex min-h-11 items-center font-display text-2xl uppercase tracking-widest text-text hover:text-white"
        >
          Subsolo
        </Link>
        <nav aria-label="Navegação" className="hidden md:flex md:gap-4">
          <Link href={city ? `/${city}` : "/"} className={navLink}>
            Agenda
          </Link>
          <Link href="/apoie" className={navLink}>
            Apoie
          </Link>
        </nav>
        {cities.length > 0 && (
          <CitySwitcher
            city={city}
            cities={cities}
            onCityChange={onCityChange}
          />
        )}
      </div>
    </header>
  );
}
