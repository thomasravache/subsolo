import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { MyShow, ShowForEdit } from "./shows";

/**
 * SPEC-0023 — dados das telas de shows, no servidor, com o cookie da requisição recebida repassado à API (o Next só o
 * encaminha): "Meus shows" (`GET /api/v1/me/shows`, próximos e passados) e o show da tela de edição
 * (`GET /api/v1/shows/{id}`). API fora do ar ou com erro → `undefined` (a página mostra o aviso); show inexistente ou
 * de outro perfil → `"not-found"`.
 */
export interface MyShowsData {
  upcoming: MyShow[];
  past: MyShow[];
}

const options = async () => ({
  signal: AbortSignal.timeout(apiTimeoutMs),
  cache: "no-store" as const,
  headers: { cookie: (await headers()).get("cookie") ?? "" },
});

/** "Meus shows" de quem pede a página: os próximos e os passados. */
export async function getMyShows(): Promise<MyShowsData | undefined> {
  try {
    const client = await apiClient();
    const [upcoming, past] = await Promise.all([
      client.GET("/api/v1/me/shows", {
        ...(await options()),
        params: { query: { period: "upcoming" } },
      }),
      client.GET("/api/v1/me/shows", {
        ...(await options()),
        params: { query: { period: "past" } },
      }),
    ]);
    return upcoming.data && past.data
      ? { upcoming: upcoming.data, past: past.data }
      : undefined;
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}

/** O show para a tela de edição. */
export async function getShowForEdit(
  id: string,
): Promise<ShowForEdit | "not-found" | undefined> {
  try {
    const client = await apiClient();
    const { data, response } = await client.GET("/api/v1/shows/{id}", {
      ...(await options()),
      params: { path: { id } },
    });
    if (data) {
      return data;
    }
    return [400, 403, 404].includes(response.status) ? "not-found" : undefined;
  } catch (error) {
    unstable_rethrow(error);
    return undefined;
  }
}

/** Um dia sem shows: a agenda dele traz só a cidade (com o fuso). */
const emptyDay = "2000-01-01";

/**
 * O fuso (IANA) de cada cidade de `slugs`, pela agenda pública de um dia passado (só a cidade volta); a cidade que
 * não responder fica de fora.
 */
export async function getCityTimeZones(
  slugs: string[],
): Promise<Record<string, string>> {
  const unique = [...new Set(slugs)];
  const zones: Record<string, string> = {};
  try {
    const client = await apiClient();
    const answers = await Promise.all(
      unique.map((slug) =>
        client.GET("/api/v1/cities/{slug}/agenda", {
          params: { path: { slug }, query: { from: emptyDay, to: emptyDay } },
          signal: AbortSignal.timeout(apiTimeoutMs),
          cache: "no-store",
        }),
      ),
    );
    for (const { data } of answers) {
      if (data) {
        zones[data.city.slug] = data.city.timeZone;
      }
    }
  } catch (error) {
    unstable_rethrow(error);
  }
  return zones;
}
