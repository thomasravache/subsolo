import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { getMyProfiles, getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { Announcement } from "@/components/profiles/Announcement";
import {
  claimPath,
  MyProfilesList,
  requestPath,
} from "@/components/profiles/MyProfilesList";
import {
  profileMessages,
  requestSentNotice,
} from "@/components/profiles/messages";
import { buttonClasses } from "@/components/ui/Button";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../../entrar/account-layout";
import { canManageShows } from "../shows/access";

/**
 * SPEC-0021 — `/conta/perfis` (tela "Meus perfis - Subsolo" do Stitch, sem "Editar perfil" e "ver painel"): os perfis
 * do usuário e a situação dos pedidos ("EM ANÁLISE", "APROVADO", "RECUSADO" com o motivo e "Pedir novamente"), com
 * "Pedir novo perfil" e "Reivindicar perfil existente" (SPEC-0023: e "Meus shows", para quem tem perfil aprovado). `?aviso=pedido-enviado` anuncia "Pedido enviado.". Renderizada
 * por requisição com a sessão do cookie e servida com `no-store` (pelo `proxy`); sem sessão →
 * `/entrar?returnTo=/conta/perfis`.
 */
export const metadata: Metadata = { title: "Meus perfis | Subsolo" };

export const instant = false;

const path = "/conta/perfis";

export default async function MyProfilesPage({
  searchParams,
}: PageProps<"/conta/perfis">) {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(path)}`);
  }
  const { aviso } = await searchParams;
  const [mine, form] =
    session.kind === "signed-in"
      ? await Promise.all([getMyProfiles(), getProfileFormData()])
      : [undefined, undefined];

  return (
    <AccountMain>
      <h1 className={accountTitle}>Meus perfis</h1>
      <Announcement
        message={
          aviso === requestSentNotice ? profileMessages.requestSent : null
        }
      />
      {mine && form ? (
        <>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Link href={requestPath} className={buttonClasses("primary", "md")}>
              Pedir novo perfil
            </Link>
            <Link href={claimPath} className={buttonClasses("secondary", "md")}>
              Reivindicar perfil existente
            </Link>
          </div>
          {session.kind === "signed-in" && canManageShows(session.me) && (
            <Link
              href="/conta/shows"
              className={buttonClasses("secondary", "md", "self-start")}
            >
              Meus shows
            </Link>
          )}
          <MyProfilesList data={mine} cities={form.cities} />
        </>
      ) : (
        <p role="alert" className={accountText}>
          {profileMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
