import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getMyFollowsForPage } from "@/api/follows-data";
import { getNotificationPreferencesForPage } from "@/api/notifications-data";
import { getSession } from "@/api/session";
import { FollowingList } from "@/components/engagement/FollowingList";
import { followMessages, signInHref } from "@/components/engagement/messages";
import { NoticesPanel } from "@/components/notifications/NoticesPanel";
import { noticeMessages } from "@/components/notifications/messages";

import { AccountMain, accountTitle } from "../entrar/account-layout";

/**
 * SPEC-0027 — `/seguindo`, a aba "Seguindo" da navegação inferior (parte "Seguindo" da tela "Avisos e Seguindo" do
 * Stitch): as cidades, bandas, casas e produtores que o fã segue, com "Deixar de seguir". SPEC-0030 — em cima, a parte
 * "Avisos" ({@link NoticesPanel}): ativar o push neste aparelho e os quatro interruptores de aviso. Renderizada por requisição com a sessão do cookie e servida com `Cache-Control: no-store` (pelo `proxy`: o
 * que o fã segue é dado pessoal); sem sessão → `/entrar?returnTo=/seguindo`.
 */
export const metadata: Metadata = { title: "Seguindo | Subsolo" };

export const instant = false;

const path = "/seguindo";

export default async function FollowingPage() {
  const session = await getSession();
  if (session.kind === "anonymous") {
    redirect(signInHref(path));
  }
  const signedIn = session.kind === "signed-in";
  const [follows, preferences] = signedIn
    ? await Promise.all([
        getMyFollowsForPage(),
        getNotificationPreferencesForPage(),
      ])
    : [undefined, undefined];

  return (
    <AccountMain>
      <h1 className={accountTitle}>Seguindo</h1>
      {preferences ? (
        <NoticesPanel initial={preferences} />
      ) : (
        <section
          aria-labelledby="avisos-titulo"
          className="flex flex-col gap-3"
        >
          <h2
            id="avisos-titulo"
            className="border-b border-border pb-2 font-display text-xl uppercase leading-none tracking-wide text-text"
          >
            {noticeMessages.title}
          </h2>
          <p role="alert" className="text-base text-text-muted">
            {noticeMessages.unavailable}
          </p>
        </section>
      )}
      {follows ? (
        <FollowingList initial={follows} />
      ) : (
        <p role="alert" className="text-base text-text-muted">
          {followMessages.unavailable}
        </p>
      )}
    </AccountMain>
  );
}
