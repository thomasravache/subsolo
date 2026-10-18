/** SPEC-0011 — id do `<main>` de cada página pública, alvo do link "Pular para o conteúdo" do shell. */
export const mainContentId = "conteudo";

/** SPEC-0016 — páginas de entrar e da conta (`/entrar`, `/entrar/confirmar`, `/conta`). */
export function isAccountPage(pathname: string): boolean {
  return /^\/(?:entrar|conta)(?:\/|$)/.test(pathname);
}

/** SPEC-0017 — páginas do painel do admin (`/admin/…`): status decidido pelo `proxy` com a sessão (ADR-0016). */
export function isAdminPage(pathname: string): boolean {
  return /^\/admin(?:\/|$)/.test(pathname);
}

/**
 * SPEC-0016 — destino de "Conta"/"Entrar": `/conta` com sessão; sem sessão, `/entrar`, com `?returnTo=` da página
 * atual (quando há uma página para onde voltar; nunca as próprias páginas de entrar e da conta).
 */
export function accountHref(signedIn: boolean, returnTo?: string): string {
  if (signedIn) {
    return "/conta";
  }
  return returnTo && returnTo !== "/" && !isAccountPage(returnTo)
    ? `/entrar?returnTo=${encodeURIComponent(returnTo)}`
    : "/entrar";
}
