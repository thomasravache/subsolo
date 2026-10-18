import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { ShowForm } from "@/components/shows/ShowForm";
import {
  myShowsPath,
  newShowPath,
  showMessages,
} from "@/components/shows/messages";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../../../entrar/account-layout";
import { canManageShows, isAdmin } from "../access";
import { getOrganizers } from "../organizers";

/**
 * SPEC-0023 — `/conta/shows/novo` (tela "Cadastrar Show - Subsolo" do Stitch): "CADASTRAR SHOW" e o formulário;
 * "Publicar show" → "Show publicado." em "Meus shows". Sem sessão → `/entrar?returnTo=/conta/shows/novo`; sem perfil
 * aprovado → o convite para pedir o perfil.
 */
export const metadata: Metadata = { title: "Cadastrar show | Subsolo" };

export const instant = false;

export default async function NewShowPage() {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(newShowPath)}`);
  }
  const allowed = session.kind === "signed-in" && canManageShows(session.me);
  const [data, organizers] = allowed
    ? await Promise.all([getProfileFormData(), getOrganizers()])
    : [undefined, undefined];

  return (
    <AccountMain>
      <Link
        href={myShowsPath}
        className="self-start text-sm text-text-muted underline underline-offset-4 hover:text-text"
      >
        ← Meus shows
      </Link>
      <h1 className={accountTitle}>Cadastrar show</h1>
      {session.kind === "signed-in" && !allowed ? (
        <p className={accountText}>{showMessages.noProfile}</p>
      ) : data && organizers ? (
        <>
          <p className={accountText}>
            Preencha os dados e o show entra na hora na agenda da cidade.
          </p>
          <ShowForm
            mode="create"
            cities={data.cities}
            styles={data.styles}
            organizers={organizers}
            isAdmin={session.kind === "signed-in" && isAdmin(session.me)}
          />
        </>
      ) : (
        <p role="alert" className={accountText}>
          {showMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
