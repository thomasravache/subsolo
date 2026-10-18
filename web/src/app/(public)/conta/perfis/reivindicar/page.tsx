import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { ClaimSearch } from "@/components/profiles/ClaimSearch";
import { profileMessages } from "@/components/profiles/messages";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../../../entrar/account-layout";

/**
 * SPEC-0021 — `/conta/perfis/reivindicar` (tela "Reivindicar Perfil - Subsolo" do Stitch): busca pelos perfis sem dono
 * (selo "SEM DONO"), a prova e "Enviar pedido" → "Pedido enviado." em "Meus perfis". Sem sessão →
 * `/entrar?returnTo=/conta/perfis/reivindicar`.
 */
export const metadata: Metadata = { title: "Reivindicar perfil | Subsolo" };

export const instant = false;

const path = "/conta/perfis/reivindicar";

export default async function ClaimProfilePage() {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(path)}`);
  }
  const data =
    session.kind === "signed-in" ? await getProfileFormData() : undefined;

  return (
    <AccountMain>
      <h1 className={accountTitle}>Reivindicar perfil</h1>
      {data ? (
        <>
          <p className={accountText}>
            Sua banda, casa ou produtora já está no Subsolo sem dono? Peça para
            assumir o perfil, com todo o histórico de shows. O admin da cena
            confirma.
          </p>
          <ClaimSearch cities={data.cities} />
        </>
      ) : (
        <p role="alert" className={accountText}>
          {profileMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
