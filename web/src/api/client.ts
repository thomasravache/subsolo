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

export function apiClient() {
  return createClient<paths>({ baseUrl: apiBaseUrl() });
}
