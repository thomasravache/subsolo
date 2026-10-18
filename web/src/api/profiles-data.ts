import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import type { Style } from "./admin";
import { apiClient, apiTimeoutMs, type CitySummary } from "./client";
import type { MyProfiles, QueueItem } from "./profiles";

/**
 * SPEC-0021 — dados das telas de perfis, no servidor, com o cookie da requisição recebida repassado à API (o Next só o
 * encaminha): "Meus perfis" (`GET /api/v1/me/profiles`), a fila do admin (`GET /api/v1/admin/profile-requests`) e as
 * cidades e estilos dos formulários (públicos). Com a API fora do ar ou com erro, `undefined`: a página mostra o aviso
 * de indisponível.
 */
export interface ProfileFormData {
  cities: CitySummary[];
  styles: Style[];
}

const options = () => ({
  signal: AbortSignal.timeout(apiTimeoutMs),
  cache: "no-store" as const,
});

async function cookieHeader(): Promise<{ cookie: string }> {
  return { cookie: (await headers()).get("cookie") ?? "" };
}

async function attempt<T>(
  load: () => Promise<T | undefined>,
): Promise<T | undefined> {
  try {
    return await load();
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante.
    unstable_rethrow(error);
    return undefined;
  }
}

/** Cidades e estilos (para os formulários e para mostrar o nome da cidade em vez do slug). */
export function getProfileFormData(): Promise<ProfileFormData | undefined> {
  return attempt(async () => {
    const client = await apiClient();
    const [cities, styles] = await Promise.all([
      client.GET("/api/v1/cities", options()),
      client.GET("/api/v1/styles", options()),
    ]);
    return cities.data && styles.data
      ? { cities: cities.data, styles: styles.data }
      : undefined;
  });
}

/** "Meus perfis" de quem pede a página. */
export function getMyProfiles(): Promise<MyProfiles | undefined> {
  return attempt(async () => {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/me/profiles", {
      ...options(),
      headers: await cookieHeader(),
    });
    return data;
  });
}

/** A fila de pedidos de perfil do admin. */
export function getProfileQueue(): Promise<QueueItem[] | undefined> {
  return attempt(async () => {
    const client = await apiClient();
    const { data } = await client.GET("/api/v1/admin/profile-requests", {
      ...options(),
      headers: await cookieHeader(),
    });
    return data;
  });
}
