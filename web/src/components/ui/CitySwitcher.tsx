"use client";

import { useId } from "react";

/** Cidade oferecida no seletor: `slug` da URL (`/{slug}`) e nome exibido. */
export interface CityOption {
  slug: string;
  name: string;
}

/**
 * Seletor de cidade do cabeçalho: mostra a cidade atual (`city`, slug) entre `cities` e chama `onCityChange` com o
 * slug escolhido.
 */
export interface CitySwitcherProps {
  city: string;
  cities: CityOption[];
  onCityChange: (slug: string) => void;
}

/**
 * SPEC-0011 — seletor nativo (`<select>` com rótulo "Cidade"): acessível por teclado e leitor de tela sem script
 * extra. Cidade atual fora da lista (ex.: página de cidade inexistente) → opção vazia "Escolha a cidade".
 */
export function CitySwitcher({
  city,
  cities,
  onCityChange,
}: CitySwitcherProps) {
  const id = useId();
  const known = cities.some((option) => option.slug === city);
  return (
    <div className="flex items-center">
      <label htmlFor={id} className="sr-only">
        Cidade
      </label>
      <select
        id={id}
        value={known ? city : ""}
        onChange={(event) => onCityChange(event.target.value)}
        className="min-h-11 min-w-11 max-w-48 cursor-pointer truncate rounded-md border border-border bg-surface px-3 font-label text-xs font-semibold uppercase tracking-wider text-text hover:border-accent"
      >
        {!known && (
          <option value="" disabled>
            Escolha a cidade
          </option>
        )}
        {cities.map((option) => (
          <option key={option.slug} value={option.slug}>
            {option.name}
          </option>
        ))}
      </select>
    </div>
  );
}
