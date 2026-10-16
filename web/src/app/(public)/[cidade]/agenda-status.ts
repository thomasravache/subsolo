import { headers } from "next/headers";

/**
 * SPEC-0008 — cabeçalho de requisição com que o `proxy` (que já definiu o status HTTP) avisa a página de que a
 * cidade não existe (`not-found`, 404) ou de que a API está fora do ar (`unavailable`, 503).
 */
export const agendaStatusHeader = "x-subsolo-agenda";

export type AgendaStatus = "not-found" | "unavailable";

export async function agendaStatus(): Promise<AgendaStatus | undefined> {
  const status = (await headers()).get(agendaStatusHeader);
  return status === "not-found" || status === "unavailable"
    ? status
    : undefined;
}
