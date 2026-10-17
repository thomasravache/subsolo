import { apiClient, apiTimeoutMs, type CitySummary } from "./client";

/**
 * SPEC-0011 — cidades do seletor do cabeçalho (`GET /api/v1/cities`, ordenadas pelo nome). Com a API fora do ar ou
 * com erro, devolve lista vazia: o shell continua de pé, só sem o seletor.
 */
export async function listCities(): Promise<CitySummary[]> {
  try {
    const { data } = await apiClient().GET("/api/v1/cities", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return data ?? [];
  } catch {
    return [];
  }
}
