import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { MyFollows } from "./follows";

/**
 * SPEC-0027 — dados de seguir no servidor: a contagem pública de seguidores de um perfil (`GET
 * /api/v1/profiles/{profileId}/followers`) e o que o fã segue (`GET /api/v1/me/follows`, com o cookie da requisição
 * recebida repassado à API, como "Meus perfis"). Com a API fora do ar ou com erro, `undefined`: a página segue de pé
 * (sem a contagem, ou com o aviso de indisponível).
 */
const options = () => ({
  signal: AbortSignal.timeout(apiTimeoutMs),
  cache: "no-store" as const,
});

async function attempt<T>(
  load: () => Promise<T | undefined>,
): Promise<T | undefined> {
  try {
    return await load();
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}

/** Quantos seguem o perfil (público, só o número). */
export function getProfileFollowerCount(
  profileId: string,
): Promise<number | undefined> {
  return attempt(async () => {
    const client = await apiClient();
    const { data } = await client.GET(
      "/api/v1/profiles/{profileId}/followers",
      { params: { path: { profileId } }, ...options() },
    );
    return data?.count;
  });
}

/** O que segue quem pede a página. */
export function getMyFollowsForPage(): Promise<MyFollows | undefined> {
  return attempt(async () => {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/me/follows", {
      ...options(),
      headers: { cookie: (await headers()).get("cookie") ?? "" },
    });
    return data;
  });
}
