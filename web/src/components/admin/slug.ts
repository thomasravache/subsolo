/**
 * SPEC-0017 — prévia do endereço (slug) gerado do nome, igual à da API: sem acento, minúsculas, cada sequência fora de
 * `[a-z0-9]` vira um hífen, sem hífen nas pontas, até 60 caracteres (ex.: "São José dos Pinhais" →
 * `sao-jose-dos-pinhais`). Quem vale é a API: a tela só mostra a prévia.
 */
export function slugFromName(name: string): string {
  return name
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 60)
    .replace(/-+$/, "");
}

/** Domínio público do Subsolo, na prévia do endereço da cidade. */
export const publicDomain = "subsolo.app";
