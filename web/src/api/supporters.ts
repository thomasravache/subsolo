import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";

/**
 * SPEC-0028 — a lista pública de apoiadores (`GET /api/v1/supporters`) para a página `/apoie`, pelo cliente tipado. Só
 * roda no servidor; com o IP do visitante em `X-Forwarded-For` (SPEC-0014), como as outras leituras públicas.
 */
export interface Supporter {
  id: string;
  name: string;
  url: string | null;
}

/** `ok` com os apoiadores na ordem de cadastro; `unavailable` com a API fora do ar ou com erro 5xx. */
export type SupportersResult =
  { kind: "ok"; supporters: Supporter[] } | { kind: "unavailable" };

export async function getSupporters(): Promise<SupportersResult> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/supporters", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    return data
      ? {
          kind: "ok",
          supporters: data.map(({ id, name, url }) => ({
            id,
            name,
            url: url ?? null,
          })),
        }
      : { kind: "unavailable" };
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return { kind: "unavailable" };
  }
}
