import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { components } from "./schema";

/**
 * SPEC-0024 — o corpo de `GET /api/v1/cities/{city}/shows/{slug}` (o tipo gerado do OpenAPI, Emenda v2 e), para a
 * página do show e os metadados Open Graph.
 */
export type ShowDetail = components["schemas"]["ShowDetailResponse"];

/**
 * SPEC-0024 — o show da página `/{cidade}/{show}`:
 * - `ok`: o show (`GET /api/v1/cities/{city}/shows/{slug}`);
 * - `not-found`: show ou cidade inexistente (404) ou endereço fora do padrão (400);
 * - `unavailable`: API fora do ar (conexão recusada, tempo esgotado) ou com erro 5xx.
 * Com o IP do visitante em `X-Forwarded-For` (SPEC-0014), como a agenda.
 */
export type ShowDetailResult =
  | { kind: "ok"; show: ShowDetail }
  | { kind: "not-found" }
  | { kind: "unavailable" };

export async function getShowDetail(
  city: string,
  slug: string,
): Promise<ShowDetailResult> {
  try {
    const client = await apiClient();
    const { data, response } = await client.GET(
      "/api/v1/cities/{city}/shows/{slug}",
      {
        params: { path: { city, slug } },
        signal: AbortSignal.timeout(apiTimeoutMs),
        cache: "no-store",
      },
    );
    if (data) {
      return { kind: "ok", show: data };
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
