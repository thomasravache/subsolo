import type { Metadata } from "next";
import { redirect } from "next/navigation";
import type { ReactNode } from "react";

import { getSession } from "@/api/session";
import { DeleteAccountDialog } from "@/components/account/DeleteAccountDialog";
import { DisplayNameForm } from "@/components/account/DisplayNameForm";
import { ExportDataButton } from "@/components/account/ExportDataButton";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../entrar/account-layout";
import { SignOutButton } from "./sign-out-button";

/**
 * SPEC-0016/SPEC-0019 — `/conta` (tela "Minha Conta - Subsolo" do Stitch, sem a engrenagem do topo, sem o selo
 * "Entrou com Google" e com "Sessão atual" só com "Sair da conta"): "Seus dados" (e-mail só leitura e nome de
 * exibição), "Privacidade & dados" (baixar os dados), "Sessão atual" e "Excluir conta". Renderizada por requisição com
 * a sessão do cookie e servida com `Cache-Control: no-store` (pelo `proxy`); sem sessão → `/entrar?returnTo=/conta`.
 */
export const metadata: Metadata = { title: "Minha conta | Subsolo" };

export const instant = false;

const sectionHeading = "font-label text-xs font-bold uppercase tracking-wider";

/** Uma seção da conta: card com título em rótulo (`h2`), ligado à região por `aria-labelledby`. */
function AccountSection({
  id,
  title,
  danger = false,
  children,
}: {
  id: string;
  title: string;
  danger?: boolean;
  children: ReactNode;
}) {
  return (
    <section
      aria-labelledby={id}
      className={`flex flex-col gap-3 rounded-md border bg-surface p-4 ${danger ? "border-danger" : "border-border"}`}
    >
      <h2
        id={id}
        className={`${sectionHeading} ${danger ? "text-danger" : "text-text-muted"}`}
      >
        {title}
      </h2>
      {children}
    </section>
  );
}

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
      <AccountSection id="seus-dados" title="Seus dados">
        <dl className="flex flex-col gap-1">
          <dt className="text-sm text-text-muted">E-mail cadastrado</dt>
          <dd className="break-all text-base text-text">{session.me.email}</dd>
        </dl>
        <DisplayNameForm initialName={session.me.displayName ?? null} />
      </AccountSection>
      <AccountSection id="privacidade-e-dados" title="Privacidade & dados">
        <p className="text-sm text-text-muted">
          Baixe uma cópia dos seus dados salvos na plataforma, incluindo
          histórico de presença em shows e bandas seguidas.
        </p>
        <ExportDataButton />
      </AccountSection>
      <AccountSection id="sessao-atual" title="Sessão atual">
        <SignOutButton />
      </AccountSection>
      <AccountSection id="excluir-conta" title="Excluir conta" danger>
        <p className="text-sm text-text-muted">
          Atenção: esta ação é irreversível. Todos os seus dados, histórico de
          confirmações de presença e preferências serão permanentemente apagados
          dos servidores do Subsolo.
        </p>
        <DeleteAccountDialog />
      </AccountSection>
    </AccountMain>
  );
}
