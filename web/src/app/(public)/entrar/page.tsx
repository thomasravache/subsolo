import type { Metadata } from "next";
import { redirect } from "next/navigation";

import { getAuthProviders } from "@/api/providers";
import { getSession } from "@/api/session";

import { SignInForm } from "./sign-in-form";

/**
 * SPEC-0016 — `/entrar`: o fã digita o e-mail e recebe o link de acesso (tela "Entrar - Subsolo" do Stitch, sem o selo
 * "ANTI-SPAM" e sem "IDENTIFICAÇÃO RÁPIDA"). Quem já está logado vai para `/conta`. O `returnTo` da URL segue para a
 * API, que o saneia (só caminho relativo do próprio site). SPEC-0020 — "Continuar com Google" só com o Google ligado na
 * API (`GET /api/v1/auth/providers`); `?erro=google` e `?erro=google_email` mostram a mensagem da volta do Google.
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
  const [{ returnTo, erro }, providers] = await Promise.all([
    searchParams,
    getAuthProviders(),
  ]);
  return (
    <SignInForm
      returnTo={typeof returnTo === "string" ? returnTo : null}
      google={providers.google}
      error={typeof erro === "string" ? erro : null}
    />
  );
}
