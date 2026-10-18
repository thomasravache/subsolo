import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";

import { getProfileFormData } from "@/api/profiles-data";
import { getSession } from "@/api/session";
import { getCityTimeZones, getMyShows } from "@/api/shows-data";
import { Announcement } from "@/components/profiles/Announcement";
import { MyShowsList } from "@/components/shows/MyShowsList";
import {
  myShowsPath,
  newShowPath,
  showMessages,
  showNotices,
} from "@/components/shows/messages";
import { buttonClasses } from "@/components/ui/Button";

import {
  AccountMain,
  accountText,
  accountTitle,
} from "../../entrar/account-layout";
import { canManageShows } from "./access";

/**
 * SPEC-0023 — `/conta/shows` (tela "Meus Shows - Produtor - Subsolo" do Stitch, sem o selo "produtor verificado"):
 * "MEUS SHOWS", "+ NOVO SHOW" e as abas "PRÓXIMOS" / "PASSADOS" com os cartões e as ações. `?aviso=` anuncia "Show
 * publicado." ou "Alterações salvas.". Renderizada por requisição com a sessão do cookie e servida com `no-store` (pelo
 * `proxy`); sem sessão → `/entrar?returnTo=/conta/shows`; sem perfil aprovado → o convite para pedir o perfil.
 */
export const metadata: Metadata = { title: "Meus shows | Subsolo" };

export const instant = false;

export default async function MyShowsPage({
  searchParams,
}: PageProps<"/conta/shows">) {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(`/entrar?returnTo=${encodeURIComponent(myShowsPath)}`);
  }
  const { aviso } = await searchParams;
  const notice =
    typeof aviso === "string" ? (showNotices[aviso] ?? null) : null;

  if (session.kind === "signed-in" && !canManageShows(session.me)) {
    return (
      <AccountMain>
        <h1 className={accountTitle}>Meus shows</h1>
        <p className={accountText}>{showMessages.noProfile}</p>
        <Link
          href="/conta/perfis"
          className={buttonClasses("secondary", "md", "self-start")}
        >
          Meus perfis
        </Link>
      </AccountMain>
    );
  }

  const [shows, form] =
    session.kind === "signed-in"
      ? await Promise.all([getMyShows(), getProfileFormData()])
      : [undefined, undefined];
  const timeZones = shows
    ? await getCityTimeZones(
        [...shows.upcoming, ...shows.past].map((show) => show.city),
      )
    : {};

  return (
    <AccountMain>
      <div className="flex flex-col gap-1">
        <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
          Gerenciamento
        </p>
        <h1 className={accountTitle}>Meus shows</h1>
      </div>
      <Announcement message={notice} />
      {shows && form ? (
        <>
          <Link
            href={newShowPath}
            className={buttonClasses("primary", "lg", "self-stretch")}
          >
            + Novo show
          </Link>
          <MyShowsList
            upcoming={shows.upcoming}
            past={shows.past}
            cities={form.cities}
            timeZones={timeZones}
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
