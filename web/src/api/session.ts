import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";

import { apiClient, apiTimeoutMs } from "./client";
import type { components } from "./schema";

/**
 * SPEC-0016 — a sessão de quem pede a página, vista no servidor: `GET /api/v1/me` com o cookie da requisição recebida
 * repassado à API (o cookie `__Host-subsolo` é da API; o Next só o encaminha, nunca o lê nem o guarda). Sem o cookie,
 * nem consulta a API.
 */
export type Me = components["schemas"]["MeResponse"];

export type SessionResult =
  | { kind: "signed-in"; me: Me }
  | { kind: "anonymous" }
  | { kind: "unavailable" };

/** Nome do cookie de sessão da API (ADR-0003). */
export const sessionCookieName = "__Host-subsolo";

export async function getSession(): Promise<SessionResult> {
  const cookie = (await headers()).get("cookie") ?? "";
  if (!cookie.includes(`${sessionCookieName}=`)) {
    return { kind: "anonymous" };
  }
  try {
    const client = await apiClient();
    const { data, response } = await client.GET("/api/v1/me", {
      headers: { cookie },
      signal: AbortSignal.timeout(apiTimeoutMs),
      cache: "no-store",
    });
    if (data) {
      return { kind: "signed-in", me: data };
    }
    return response.status === 401
      ? { kind: "anonymous" }
      : { kind: "unavailable" };
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, redirecionamento) seguem adiante.
    unstable_rethrow(error);
    return { kind: "unavailable" };
  }
}
