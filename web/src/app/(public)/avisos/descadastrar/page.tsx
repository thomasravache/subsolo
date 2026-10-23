import type { Metadata } from "next";

import { UnsubscribeConfirmation } from "@/components/notifications/UnsubscribeConfirmation";

import { AccountMain, accountTitle } from "../../entrar/account-layout";

/**
 * SPEC-0030 — `/avisos/descadastrar?token=…`, o link de todo e-mail de aviso (ADR-0008, ADR-0014): sem login, manda o
 * token à API e confirma o tipo de e-mail desligado (lido do próprio token); link adulterado ou fora do formato →
 * "Este link de descadastro é inválido.". Servida pelo `proxy` sem cache (o token vai no endereço).
 */
export const metadata: Metadata = {
  title: "Descadastrar e-mail | Subsolo",
  robots: { index: false, follow: false },
  referrer: "no-referrer",
};

export const instant = false;

export default async function UnsubscribePage({
  searchParams,
}: PageProps<"/avisos/descadastrar">) {
  const { token } = await searchParams;
  return (
    <AccountMain>
      <h1 className={accountTitle}>Avisos por e-mail</h1>
      <UnsubscribeConfirmation
        token={typeof token === "string" ? token : undefined}
      />
    </AccountMain>
  );
}
