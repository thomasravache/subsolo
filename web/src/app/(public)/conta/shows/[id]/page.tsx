import type { Metadata } from "next";
import Link from "next/link";
import { notFound, redirect } from "next/navigation";

import { getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { getShowForEdit } from "@/api/shows-data";
import { ShowForm } from "@/components/shows/ShowForm";
import {
  editShowPath,
  myShowsPath,
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
 * SPEC-0023 — `/conta/shows/{id}` ("Editar show", a mesma tela do cadastro): o formulário com os dados do show;
 * "Salvar" → "Alterações salvas." em "Meus shows". Show inexistente ou de outro perfil → página 404. Sem sessão →
 * `/entrar?returnTo=/conta/shows/{id}`.
 */
export const metadata: Metadata = { title: "Editar show | Subsolo" };

export const instant = false;

export default async function EditShowPage({
  params,
}: PageProps<"/conta/shows/[id]">) {
  const { id } = await params;
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(editShowPath(id))}`);
  }
  const allowed = session.kind === "signed-in" && canManageShows(session.me);
  const [show, data, organizers] = allowed
    ? await Promise.all([
        getShowForEdit(id),
        getProfileFormData(),
        getOrganizers(),
      ])
    : [undefined, undefined, undefined];
  if (show === "not-found") {
    notFound();
  }

  return (
    <AccountMain>
      <Link
        href={myShowsPath}
        className="self-start text-sm text-text-muted underline underline-offset-4 hover:text-text"
      >
        ← Meus shows
      </Link>
      <h1 className={accountTitle}>Editar show</h1>
      {session.kind === "signed-in" && !allowed ? (
        <p className={accountText}>{showMessages.noProfile}</p>
      ) : show && data && organizers ? (
        <ShowForm
          mode="edit"
          cities={data.cities}
          styles={data.styles}
          organizers={organizers}
          isAdmin={session.kind === "signed-in" && isAdmin(session.me)}
          show={show}
        />
      ) : (
        <p role="alert" className={accountText}>
          {showMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
