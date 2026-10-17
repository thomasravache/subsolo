"use client";

import type { ReactNode } from "react";

export interface ChipProps {
  selected: boolean;
  onToggle: () => void;
  children: ReactNode;
}

/** SPEC-0011 — filtro liga/desliga (ex.: estilo, período), com `aria-pressed` e alvo de toque ≥ 44 px. */
export function Chip({ selected, onToggle, children }: ChipProps) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onToggle}
      className={`inline-flex min-h-11 items-center whitespace-nowrap rounded-md border px-3.5 font-label text-xs font-bold uppercase tracking-wider transition-colors ${
        selected
          ? "border-accent bg-accent text-text-on-accent"
          : "border-border bg-surface text-text hover:border-text-muted"
      }`}
    >
      {children}
    </button>
  );
}
