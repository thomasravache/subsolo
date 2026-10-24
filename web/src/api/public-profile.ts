import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { components } from "./schema";

/**
 * SPEC-0026 — perfil público de banda, casa ou produtor para as páginas `/banda|casa|produtor/{slug}`, e os shows dele,
 * pelo cliente tipado (tipos gerados do OpenAPI). Só roda no servidor, com o IP do visitante em `X-Forwarded-For`
 * (SPEC-0014), como a agenda e a página do show.
 */
export type PublicProfileType = "band" | "venue" | "producer";

/** O corpo de `GET /api/v1/profiles/{type}/{slug}` (§6). */
export type PublicProfile = components["schemas"]["PublicProfileResponse"];

/** Um item de `GET /api/v1/shows?profileId=&period=`: o resumo da agenda (SPEC-0008) e a cidade do show (Emenda v2, b). */
export type ProfileShowItem = components["schemas"]["ProfileShowResponse"];

/**
 * O que os metadados precisam de um show do perfil: o resumo da agenda (a cidade e o `flyerId`, que só a lista traz,
 * são opcionais aqui; SPEC-0031, H26-03b: a imagem Open Graph vem do `flyerId`).
 */
export type ProfileShow = components["schemas"]["ShowSummaryResponse"] &
  Partial<Pick<ProfileShowItem, "city" | "flyerId">>;

/**
 * - `ok`: o perfil aprovado;
 * - `not-found`: inexistente, pendente, recusado ou de outro tipo (404) ou endereço fora do padrão (400);
 * - `unavailable`: API fora do ar (conexão recusada, tempo esgotado) ou com erro 5xx.
 */
export type PublicProfileResult =
  | { kind: "ok"; profile: PublicProfile }
  | { kind: "not-found" }
  | { kind: "unavailable" };

/** `ok` com os shows; `unavailable` quando o endpoint de shows falha (a página mostra o perfil com o aviso). */
export type ProfileShowsResult =
  { kind: "ok"; shows: ProfileShowItem[] } | { kind: "unavailable" };

export async function getPublicProfile(
  type: PublicProfileType,
  slug: string,
): Promise<PublicProfileResult> {
  try {
    const client = await apiClient();
    const { data, response } = await client.GET(
      "/api/v1/profiles/{type}/{slug}",
      {
        params: { path: { type, slug } },
        signal: AbortSignal.timeout(apiTimeoutMs),
        cache: "no-store",
      },
    );
    if (data) {
      return { kind: "ok", profile: data };
    }
    return response.status === 404 || response.status === 400
      ? { kind: "not-found" }
      : { kind: "unavailable" };
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return { kind: "unavailable" };
  }
}

export async function getProfileShows(
  profileId: string,
  period: "upcoming" | "past",
): Promise<ProfileShowsResult> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/shows", {
      params: { query: { profileId, period } },
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return data ? { kind: "ok", shows: data.shows } : { kind: "unavailable" };
  } catch (error) {
    unstable_rethrow(error);
    return { kind: "unavailable" };
  }
}
