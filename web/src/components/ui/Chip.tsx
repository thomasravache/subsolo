"use client";

import Link from "next/link";
import type { ReactNode } from "react";

export interface ChipProps {
  selected: boolean;
  onToggle: () => void;
  children: ReactNode;
}

function chipClass(selected: boolean): string {
  return `inline-flex min-h-11 items-center gap-1.5 whitespace-nowrap rounded-md border px-3.5 font-label text-xs font-bold uppercase tracking-wider transition-colors ${
    selected
      ? "border-accent bg-accent text-text-on-accent"
      : "border-border bg-surface text-text hover:border-text-muted"
  }`;
}

/** SPEC-0011 — filtro liga/desliga (ex.: estilo, período), com `aria-pressed` e alvo de toque ≥ 44 px. */
export function Chip({ selected, onToggle, children }: ChipProps) {
  return (
    <button
      type="button"
      aria-pressed={selected}
      onClick={onToggle}
      className={chipClass(selected)}
    >
      {children}
    </button>
  );
}

export interface ChipLinkProps {
  href: string;
  selected: boolean;
  children: ReactNode;
}

/**
 * SPEC-0025 — filtro que muda a URL (ex.: período e estilo da agenda): link (`next/link`, navegação no cliente) com
 * `aria-current="page"` quando selecionado, o mesmo visual do {@link Chip} e alvo de toque ≥ 44 px.
 */
export function ChipLink({ href, selected, children }: ChipLinkProps) {
  return (
    <Link
      href={href}
      aria-current={selected ? "page" : undefined}
      scroll={false}
      className={chipClass(selected)}
    >
      {children}
    </Link>
  );
}
