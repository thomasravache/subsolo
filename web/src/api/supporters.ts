/**
 * SPEC-0028 — a lista pública de apoiadores (`GET /api/v1/supporters`) para a página `/apoie`, pelo cliente tipado. Só
 * roda no servidor. Scaffold do contrato: a implementação entra no Green.
 */
export interface Supporter {
  id: string;
  name: string;
  url: string | null;
}

/** `ok` com os apoiadores na ordem de cadastro; `unavailable` com a API fora do ar ou com erro 5xx. */
export type SupportersResult =
  { kind: "ok"; supporters: Supporter[] } | { kind: "unavailable" };

export async function getSupporters(): Promise<SupportersResult> {
  throw new Error("SPEC-0028: getSupporters ainda não implementado.");
}
