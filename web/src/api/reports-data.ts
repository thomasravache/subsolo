import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { ReportQueueItem } from "./reports";

/**
 * SPEC-0033 — a fila de denúncias do admin (`GET /api/v1/admin/reports`), no servidor, com o cookie da requisição
 * recebida repassado à API (o Next só o encaminha). Com a API fora do ar ou com erro, `undefined`: a página mostra o
 * aviso de indisponível.
 */
export async function getReportQueue(): Promise<ReportQueueItem[] | undefined> {
  try {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/admin/reports", {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
      headers: { cookie: (await headers()).get("cookie") ?? "" },
    });
    return data;
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}
