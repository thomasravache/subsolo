import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";

/**
 * SPEC-0020 — provedores de login ligados na API (`GET /api/v1/auth/providers`), vistos no servidor: o botão
 * "Continuar com Google" só aparece com `google = true`. Com a API fora do ar ou com erro, o Google fica desligado (o
 * link por e-mail continua).
 */
export interface AuthProviders {
  google: boolean;
}

export async function getAuthProviders(): Promise<AuthProviders> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/auth/providers", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return { google: data?.google === true };
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return { google: false };
  }
}

/** Início do login com Google na API (mesmo domínio), com o caminho de volta (a API o saneia). */
export function googleSignInHref(returnTo: string | null): string {
  const start = "/api/v1/auth/google";
  return returnTo ? `${start}?${new URLSearchParams({ returnTo })}` : start;
}
