import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getSession } from "@/api/session";

import { SignInForm } from "./sign-in-form";

/**
 * SPEC-0016 — `/entrar`: o fã digita o e-mail e recebe o link de acesso (tela "Entrar - Subsolo" do Stitch, sem o selo
 * "ANTI-SPAM", sem "IDENTIFICAÇÃO RÁPIDA" e, até a spec do Google, sem o botão do Google). Quem já está logado vai
 * para `/conta`. O `returnTo` da URL segue para a API, que o saneia (só caminho relativo do próprio site).
 */
export const metadata: Metadata = { title: "Entrar | Subsolo" };

// Sem shell de carregamento: a página depende da sessão (o logado é redirecionado antes de qualquer conteúdo).
export const instant = false;

export default async function SignInPage({
  searchParams,
}: PageProps<"/entrar">) {
  const session = await getSession();
  if (session.kind === "signed-in") {
    redirect("/conta");
  }
  const { returnTo } = await searchParams;
  return (
    <SignInForm returnTo={typeof returnTo === "string" ? returnTo : null} />
  );
}
