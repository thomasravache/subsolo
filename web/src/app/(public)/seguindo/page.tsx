import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getMyFollowsForPage } from "@/api/follows-data";
import { getSession } from "@/api/session";
import { FollowingList } from "@/components/engagement/FollowingList";
import { followMessages, signInHref } from "@/components/engagement/messages";

import { AccountMain, accountTitle } from "../entrar/account-layout";

/**
 * SPEC-0027 — `/seguindo`, a aba "Seguindo" da navegação inferior (parte "Seguindo" da tela "Avisos e Seguindo" do
 * Stitch; os avisos chegam com a spec de push): as cidades, bandas, casas e produtores que o fã segue, com "Deixar de
 * seguir". Renderizada por requisição com a sessão do cookie e servida com `Cache-Control: no-store` (pelo `proxy`: o
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
  const follows =
    session.kind === "signed-in" ? await getMyFollowsForPage() : undefined;

  return (
    <AccountMain>
      <h1 className={accountTitle}>Seguindo</h1>
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
