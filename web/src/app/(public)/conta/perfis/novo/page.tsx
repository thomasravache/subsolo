import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { profileMessages } from "@/components/profiles/messages";
import { ProfileForm } from "@/components/profiles/ProfileForm";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../../../entrar/account-layout";

/**
 * SPEC-0021 — `/conta/perfis/novo` (tela "Pedir Perfil - Subsolo" do Stitch, com "Bandcamp / Soundcloud" virando só
 * Bandcamp): tipo, nome, cidade, estilos (só banda), links e contato, com o aviso de aprovação; "Enviar pedido" →
 * "Pedido enviado." em "Meus perfis". Sem sessão → `/entrar?returnTo=/conta/perfis/novo`.
 */
export const metadata: Metadata = { title: "Pedir perfil | Subsolo" };

export const instant = false;

const path = "/conta/perfis/novo";

export default async function RequestProfilePage() {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(path)}`);
  }
  const data =
    session.kind === "signed-in" ? await getProfileFormData() : undefined;

  return (
    <AccountMain>
      <h1 className={accountTitle}>Pedir perfil</h1>
      {data ? (
        <>
          <p className={accountText}>
            Para bandas, casas e produtores da cena. Com o perfil aprovado, você
            cadastra os seus shows.
          </p>
          <ProfileForm
            mode="request"
            cities={data.cities}
            styles={data.styles}
          />
        </>
      ) : (
        <p role="alert" className={accountText}>
          {profileMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
