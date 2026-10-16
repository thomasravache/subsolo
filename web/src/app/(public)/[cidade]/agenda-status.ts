import { headers } from "next/headers";

/**
 * SPEC-0008 — cabeçalho de requisição com que o `proxy` (que já definiu o status HTTP) diz à página o resultado
 * da agenda: `ok`, `not-found` (404) ou `unavailable` (503). O proxy sempre o sobrescreve, então o valor que o
 * cliente mandar é descartado.
 */
export const agendaStatusHeader = "x-subsolo-agenda";

export type AgendaStatus = "not-found" | "unavailable";

export async function agendaStatus(): Promise<AgendaStatus | undefined> {
  const status = (await headers()).get(agendaStatusHeader);
  return status === "not-found" || status === "unavailable"
    ? status
    : undefined;
}
