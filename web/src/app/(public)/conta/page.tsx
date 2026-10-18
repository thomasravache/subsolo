import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getSession } from "@/api/session";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../entrar/account-layout";
import { SignOutButton } from "./sign-out-button";

/**
 * SPEC-0016 — `/conta` mínima (tela "Minha Conta - Subsolo" do Stitch, só com o e-mail e "Sair"; nome, exportar e
 * excluir chegam com a spec "Minha conta"): renderizada por requisição com a sessão do cookie e servida com
 * `Cache-Control: no-store` (pelo `proxy`); sem sessão → `/entrar?returnTo=/conta`.
 */
export const metadata: Metadata = { title: "Minha conta | Subsolo" };

export const instant = false;

export default async function AccountPage() {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect("/entrar?returnTo=/conta");
  }
  if (session.kind === "unavailable") {
    return (
      <AccountMain>
        <h1 className={accountTitle}>Minha conta</h1>
        <p role="alert" className={accountText}>
          A conta está fora do ar por alguns instantes. Tente de novo.
        </p>
      </AccountMain>
    );
  }

  return (
    <AccountMain>
      <h1 className={accountTitle}>Minha conta</h1>
      <section
        aria-labelledby="seus-dados"
        className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4"
      >
        <h2
          id="seus-dados"
          className="font-label text-xs font-bold uppercase tracking-wider text-text-muted"
        >
          Seus dados
        </h2>
        <dl className="flex flex-col gap-1">
          <dt className="text-sm text-text-muted">E-mail</dt>
          <dd className="break-all text-base text-text">{session.me.email}</dd>
        </dl>
      </section>
      <SignOutButton />
    </AccountMain>
  );
}
