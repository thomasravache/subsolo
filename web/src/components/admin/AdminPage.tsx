import type { ReactNode } from "react";

import { mainContentId } from "@/components/ui/shell";

import { AdminNav } from "./AdminNav";

/** Título das páginas do painel do admin. */
export const adminPageTitle =
  "font-display text-4xl uppercase leading-none tracking-wide text-text md:text-5xl";

/** SPEC-0021 — página do painel com mensagem no lugar do conteúdo (acesso negado, painel indisponível). */
export function AdminMessage({ title, text }: { title: string; text: string }) {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-4 px-4 py-10"
    >
      <h1 className={adminPageTitle}>{title}</h1>
      <p role="alert" className="text-lg text-text-muted">
        {text}
      </p>
    </main>
  );
}

/** SPEC-0021 — o painel fora do ar ("Painel indisponível"). */
export function AdminUnavailable() {
  return (
    <AdminMessage
      title="Painel indisponível"
      text="O painel está fora do ar por alguns instantes. Tente de novo."
    />
  );
}

/** SPEC-0021 — logado sem o papel admin ("Acesso negado", 403 pelo `proxy`). */
export function AdminForbidden() {
  return (
    <AdminMessage
      title="Acesso negado"
      text="Você não tem acesso a esta página."
    />
  );
}

/** SPEC-0021 — moldura de uma página do painel: menu (com a página atual) e o conteúdo com o título. */
export function AdminPage({
  current,
  title,
  children,
}: {
  current: string;
  title: string;
  children: ReactNode;
}) {
  return (
    <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col gap-6 px-4 py-8 md:flex-row md:gap-10">
      <aside className="md:w-56 md:shrink-0">
        <AdminNav current={current} />
      </aside>
      <main id={mainContentId} className="flex min-w-0 flex-1 flex-col gap-6">
        <h1 className={adminPageTitle}>{title}</h1>
        {children}
      </main>
    </div>
  );
}
