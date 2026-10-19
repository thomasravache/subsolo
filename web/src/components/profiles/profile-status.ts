import { headers } from "next/headers";

/**
 * SPEC-0026 — cabeçalho de requisição com que o `proxy` (que já definiu o status HTTP, ADR-0016) diz à página do perfil
 * o resultado da consulta: `not-found` (404) ou `unavailable` (503). O proxy sempre o sobrescreve, então o valor que o
 * cliente mandar é descartado.
 */
export const profileStatusHeader = "x-subsolo-profile";

export type ProfileStatus = "not-found" | "unavailable";

export async function profileStatus(): Promise<ProfileStatus | undefined> {
  const status = (await headers()).get(profileStatusHeader);
  return status === "not-found" || status === "unavailable"
    ? status
    : undefined;
}
