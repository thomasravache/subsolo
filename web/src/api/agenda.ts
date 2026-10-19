import { unstable_rethrow } from "next/navigation";

import type { Style } from "./admin";
import { apiClient, apiTimeoutMs, type CityAgenda } from "./client";

/**
 * SPEC-0008 — agenda da cidade para a página `/{cidade}`:
 * - `ok`: a cidade e os shows (`GET /api/v1/cities/{slug}/agenda`);
 * - `not-found`: cidade não cadastrada (404) ou slug inválido (400), que também não é uma cidade;
 * - `unavailable`: API fora do ar (conexão recusada, tempo esgotado) ou com erro 5xx.
 * SPEC-0014: com o IP do visitante em `X-Forwarded-For` (o rate limit da API vale por visitante).
 * SPEC-0025: com os filtros (`from`, `to`, `styles`) já validados pela página.
 */
export type CityAgendaResult =
  | { kind: "ok"; agenda: CityAgenda }
  | { kind: "not-found" }
  | { kind: "unavailable" };

export interface CityAgendaQuery {
  from?: string;
  to?: string;
  styles?: string;
}

export async function getCityAgenda(
  slug: string,
  query: CityAgendaQuery = {},
): Promise<CityAgendaResult> {
  try {
    const client = await apiClient();
    const { data, response } = await client.GET(
      "/api/v1/cities/{slug}/agenda",
      {
        params: { path: { slug }, query },
        signal: AbortSignal.timeout(apiTimeoutMs),
        cache: "no-store",
      },
    );
    if (data) {
      return { kind: "ok", agenda: data };
    }
    return response.status === 404 || response.status === 400
      ? { kind: "not-found" }
      : { kind: "unavailable" };
  } catch (error) {
    // SPEC-0016 (§7.6 g): os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return { kind: "unavailable" };
  }
}

/**
 * SPEC-0025 — os estilos (`GET /api/v1/styles`, público) para os chips da agenda. Com a API fora do ar ou com erro,
 * lista vazia: a agenda aparece sem os chips de estilo.
 */
export async function getAgendaStyles(): Promise<Style[]> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/styles", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return data ?? [];
  } catch (error) {
    unstable_rethrow(error);
    return [];
  }
}
