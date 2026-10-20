import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";

/**
 * SPEC-0029 — as contagens públicas de "vão" no servidor (`GET /api/v1/attendances/counts?showIds=a,b`, só números,
 * ADR-0008), em lote: uma chamada para até {@link maxShowsPerCall} shows (a agenda e o perfil juntam com os shows do
 * `Events`, §4). Com a API fora do ar ou com erro, `undefined`: a página sai sem a contagem, sem aviso de erro (IT-05).
 */
export type AttendanceCounts = Record<string, number>;

/** Máximo de shows por chamada (§6). */
export const maxShowsPerCall = 100;

async function countsOf(ids: string[]): Promise<AttendanceCounts | undefined> {
  const client = await apiClient();
  const { data } = await client.GET("/api/v1/attendances/counts", {
    params: { query: { showIds: ids.join(",") } },
    signal: AbortSignal.timeout(apiTimeoutMs),
    cache: "no-store",
  });
  return data?.counts;
}

/** Quantos vão a cada show de `showIds` (show sem "vou" → 0), ou `undefined` se a contagem não veio. */
export async function getAttendanceCounts(
  showIds: readonly string[],
): Promise<AttendanceCounts | undefined> {
  const ids = [...new Set(showIds)];
  if (ids.length === 0) {
    return {};
  }
  try {
    const batches: string[][] = [];
    for (let start = 0; start < ids.length; start += maxShowsPerCall) {
      batches.push(ids.slice(start, start + maxShowsPerCall));
    }
    const results = await Promise.all(batches.map(countsOf));
    if (results.some((result) => result === undefined)) {
      return undefined;
    }
    const counts: AttendanceCounts = {};
    for (const id of ids) {
      counts[id] = 0;
    }
    return Object.assign(counts, ...results);
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}
