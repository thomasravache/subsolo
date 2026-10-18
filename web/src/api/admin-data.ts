import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import type { AdminCity, Style } from "./admin";
import { apiClient, apiTimeoutMs } from "./client";

/**
 * SPEC-0017 — dados iniciais do painel do admin, no servidor: as cidades (`GET /api/v1/admin/cities`, com o cookie da
 * requisição recebida repassado — o Next só o encaminha) e os estilos (`GET /api/v1/styles`, público). Com a API fora
 * do ar ou com erro, `undefined`: a página mostra o aviso de indisponível.
 */
export interface AdminPanelData {
  cities: AdminCity[];
  styles: Style[];
}

export async function getAdminPanelData(): Promise<AdminPanelData | undefined> {
  try {
    const cookie = (await headers()).get("cookie") ?? "";
    const client = await apiClient();
    const options = {
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store" as const,
    };
    const [cities, styles] = await Promise.all([
      client.GET("/api/v1/admin/cities", { ...options, headers: { cookie } }),
      client.GET("/api/v1/styles", options),
    ]);
    return cities.data && styles.data
      ? { cities: cities.data, styles: styles.data }
      : undefined;
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}
