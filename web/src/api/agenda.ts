import { apiClient, apiTimeoutMs, type CityAgenda } from "./client";

/**
 * SPEC-0008 — agenda da cidade para a página `/{cidade}`:
 * - `ok`: a cidade e os shows (`GET /api/v1/cities/{slug}/agenda`);
 * - `not-found`: cidade não cadastrada (404) ou slug inválido (400), que também não é uma cidade;
 * - `unavailable`: API fora do ar (conexão recusada, tempo esgotado) ou com erro 5xx.
 */
export type CityAgendaResult =
  | { kind: "ok"; agenda: CityAgenda }
  | { kind: "not-found" }
  | { kind: "unavailable" };

export async function getCityAgenda(slug: string): Promise<CityAgendaResult> {
  try {
    const { data, response } = await apiClient().GET(
      "/api/v1/cities/{slug}/agenda",
      {
        params: { path: { slug } },
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
  } catch {
    return { kind: "unavailable" };
  }
}
