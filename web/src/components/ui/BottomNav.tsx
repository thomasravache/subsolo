"use client";

import Link from "next/link";

import { BookmarkIcon } from "@/components/engagement/icons";

import { CalendarIcon, HeartIcon } from "./icons";
import { accountHref, followingPath } from "./shell";

export interface BottomNavProps {
  /** Slug da cidade atual (vazio fora das páginas de cidade). */
  city: string;
  /** Destino ativo, marcado com `aria-current="page"`. */
  current?: "agenda" | "seguindo" | "apoie" | "conta";
  /** SPEC-0016 — há sessão (`GET /api/v1/me`): a aba "Conta" leva a `/conta`; sem sessão, a `/entrar`. */
  signedIn?: boolean;
  /** SPEC-0016 — página atual, para a volta depois de entrar (`/entrar?returnTo=…`). */
  returnTo?: string;
}

/** SPEC-0016 — ícone da aba "Conta" (pessoa), no traço dos ícones do shell; decorativo. */
function UserIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      width="24"
      height="24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="square"
      aria-hidden="true"
      focusable="false"
    >
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21c0-4 3.6-7 8-7s8 3 8 7" />
    </svg>
  );
}

const item =
  "flex h-full min-w-11 flex-1 flex-col items-center justify-center gap-0.5 font-label text-[11px] font-bold uppercase tracking-wider";

/**
 * SPEC-0011 — navegação inferior fixa, só no celular (< 768 px); no desktop a navegação fica no cabeçalho.
 * SPEC-0016 — a aba "Conta" leva a `/conta` com sessão e a `/entrar` (com a volta para a página atual) sem sessão.
 * SPEC-0027 — abas Agenda, Seguindo, Apoie e Conta ("Buscar", fora do MVP, vira "Seguindo"); "Seguindo" leva a
 * `/seguindo` (a página manda o anônimo para `/entrar`). SPEC-0029 (H27-05): o item ativo é só o `current` recebido
 * (calculado pelo `AppShell`); a navegação não deduz o destino pela URL.
 */
export function BottomNav({
  city,
  current,
  signedIn = false,
  returnTo,
}: BottomNavProps) {
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
      {/*
        SPEC-0027 — documento novo (não a navegação do router): `/seguindo` é dado pessoal, renderizado por requisição, e a
        página anterior não fica escondida no DOM (Activity do cacheComponents) junto da lista.
      */}
      <a
        href={followingPath}
        aria-current={current === "seguindo" ? "page" : undefined}
        className={`${item} ${tone("seguindo")}`}
      >
        <BookmarkIcon />
        Seguindo
      </a>
      <Link
        href="/apoie"
        aria-current={current === "apoie" ? "page" : undefined}
        className={`${item} ${tone("apoie")}`}
      >
        <HeartIcon />
        Apoie
      </Link>
      <Link
        href={accountHref(signedIn, returnTo)}
        aria-current={current === "conta" ? "page" : undefined}
        className={`${item} ${tone("conta")}`}
      >
        <UserIcon />
        Conta
      </Link>
    </nav>
  );
}
