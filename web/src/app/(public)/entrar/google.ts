/** SPEC-0020 — início do login com Google na API (mesmo domínio), com o caminho de volta (a API o saneia). */
export function googleSignInHref(returnTo: string | null): string {
  const start = "/api/v1/auth/google";
  return returnTo ? `${start}?${new URLSearchParams({ returnTo })}` : start;
}
