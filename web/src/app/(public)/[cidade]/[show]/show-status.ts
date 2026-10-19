import { headers } from "next/headers";

/**
 * SPEC-0024 — cabeçalho de requisição com que o `proxy` (que já definiu o status HTTP, ADR-0016) diz à página do show o
 * resultado da consulta: `ok`, `not-found` (404) ou `unavailable` (503). O proxy sempre o sobrescreve, então o valor
 * que o cliente mandar é descartado.
 */
export const showStatusHeader = "x-subsolo-show";

export type ShowStatus = "not-found" | "unavailable";

export async function showStatus(): Promise<ShowStatus | undefined> {
  const status = (await headers()).get(showStatusHeader);
  return status === "not-found" || status === "unavailable"
    ? status
    : undefined;
}
