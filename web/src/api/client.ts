import { headers } from "next/headers";
import { unstable_rethrow } from "next/navigation";
import createClient from "openapi-fetch";

import type { components, paths } from "./schema";

/**
 * SPEC-0008 — cliente tipado da API .NET (ADR-0013), gerado de `docs/api/openapi.v1.json` por
 * `pnpm api:generate` (`schema.d.ts`; `pnpm api:check` confere que está em dia). Só roda no servidor: a URL vem de
 * `API_URL` (Aspire: endpoint `http` da `api`; Compose: o serviço `api` na rede interna).
 */
export type CityAgenda = components["schemas"]["CityAgendaResponse"];

/** SPEC-0011 — cidade da lista pública (`GET /api/v1/cities`): slug e nome. */
export type CitySummary = components["schemas"]["CitySummaryResponse"];

/** Tempo máximo de uma chamada à API antes de tratá-la como indisponível. */
export const apiTimeoutMs = 5_000;

export function apiBaseUrl(): string {
  const url = process.env.API_URL;
  if (!url) {
    throw new Error(
      "API_URL não configurada: o web precisa do endereço da API.",
    );
  }
  return url;
}

/** Cabeçalho com o IP do visitante, posto pelo Caddy na requisição recebida e repassado à API. */
export const forwardedForHeader = "x-forwarded-for";

/**
 * SPEC-0014 (Emenda v3) — o `X-Forwarded-For` da requisição recebida (o IP do visitante, posto pelo Caddy), lido de
 * `next/headers` — nas páginas e também no `proxy`, que roda com o escopo da requisição. Fora desse escopo ou sem o
 * cabeçalho, `undefined`.
 */
async function incomingForwardedFor(): Promise<string | undefined> {
  try {
    return (await headers()).get(forwardedForHeader) ?? undefined;
  } catch (error) {
    // Os sinais internos do Next.js (renderização dinâmica, prerender) seguem adiante; só a falta de escopo vira "sem IP".
    unstable_rethrow(error);
    return undefined;
  }
}

/**
 * Cliente da API para a requisição atual. SPEC-0014 (Emenda v3): repassa o IP do visitante em `X-Forwarded-For`,
 * para o rate limit da API valer por visitante e não para o servidor do site inteiro; sem o cabeçalho na requisição
 * recebida, não envia nenhum.
 */
export async function apiClient() {
  const visitor = await incomingForwardedFor();
  return createClient<paths>({
    baseUrl: apiBaseUrl(),
    headers: visitor ? { [forwardedForHeader]: visitor } : undefined,
  });
}
