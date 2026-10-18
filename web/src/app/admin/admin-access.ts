import { headers } from "next/headers";

/**
 * SPEC-0017 — cabeçalho de requisição com que o `proxy` (que já definiu o status HTTP, ADR-0016) diz à página do admin
 * o resultado da sessão: `ok` (admin), `forbidden` (403: logado sem o papel admin) ou `unavailable` (503: a API não
 * respondeu). Anônimo nem chega à página (307 para `/entrar`). O proxy sempre o sobrescreve, então o valor que o
 * cliente mandar é descartado.
 */
export const adminAccessHeader = "x-subsolo-admin";

export type AdminAccess = "ok" | "forbidden" | "unavailable";

/** O resultado do proxy; sem ele (nunca deveria acontecer), a página trata como sem acesso. */
export async function adminAccess(): Promise<AdminAccess> {
  const access = (await headers()).get(adminAccessHeader);
  return access === "ok" || access === "unavailable" ? access : "forbidden";
}
