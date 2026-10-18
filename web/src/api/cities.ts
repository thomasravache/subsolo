import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs, type CitySummary } from "./client";

/**
 * SPEC-0011 — cidades do seletor do cabeçalho (`GET /api/v1/cities`, ordenadas pelo nome). Com a API fora do ar ou
 * com erro, devolve lista vazia: o shell continua de pé, só sem o seletor. SPEC-0014: com o IP do visitante em
 * `X-Forwarded-For` (o rate limit da API vale por visitante).
 */
export async function listCities(): Promise<CitySummary[]> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/cities", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return data ?? [];
  } catch (error) {
    // SPEC-0016 (§7.6 g): os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return [];
  }
}
