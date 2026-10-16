import Link from "next/link";

import { CalendarIcon, HeartIcon } from "./icons";

export interface BottomNavProps {
  /** Slug da cidade atual (vazio fora das páginas de cidade). */
  city: string;
  /** Destino ativo, marcado com `aria-current="page"`. */
  current?: "agenda" | "apoie";
}

const item =
  "flex h-full min-w-11 flex-1 flex-col items-center justify-center gap-0.5 font-label text-[11px] font-bold uppercase tracking-wider";

/** SPEC-0011 — navegação inferior fixa, só no celular (< 768 px); no desktop a navegação fica no cabeçalho. */
export function BottomNav({ city, current }: BottomNavProps) {
  // Ativo: texto claro com ícone e barra no destaque (o destaque não tem contraste para texto pequeno).
  const tone = (name: BottomNavProps["current"]) =>
    current === name
      ? "text-text shadow-[inset_0_2px_0_var(--color-accent)] [&>svg]:text-accent"
      : "text-text-muted hover:text-text";
  return (
    <nav
      aria-label="Navegação inferior"
      className="fixed inset-x-0 bottom-0 z-50 flex h-16 items-stretch border-t border-border bg-surface pb-[env(safe-area-inset-bottom)] md:hidden"
    >
      <Link
        href={city ? `/${city}` : "/"}
        aria-current={current === "agenda" ? "page" : undefined}
        className={`${item} ${tone("agenda")}`}
      >
        <CalendarIcon />
        Agenda
      </Link>
      <Link
        href="/apoie"
        aria-current={current === "apoie" ? "page" : undefined}
        className={`${item} ${tone("apoie")}`}
      >
        <HeartIcon />
        Apoie
      </Link>
    </nav>
  );
}
