import type { Metadata } from "next";

import { ConfirmSignIn } from "./confirm-sign-in";

/**
 * SPEC-0016 — `/entrar/confirmar?token=…` (tela "Quase lá - Subsolo" do Stitch): carregar a página não consome o token
 * (leitores de e-mail abrem links sozinhos); só o toque em "Entrar no Subsolo" manda o token à API, no corpo do POST.
 */
export const metadata: Metadata = { title: "Entrar | Subsolo" };

export const instant = false;

export default async function ConfirmSignInPage({
  searchParams,
}: PageProps<"/entrar/confirmar">) {
  const { token } = await searchParams;
  return <ConfirmSignIn token={typeof token === "string" ? token : ""} />;
}
