import type { ApiResult } from "@/api/browser";

/**
 * SPEC-0017 — mensagens do painel do admin para os códigos da API (§6). Os campos de validação vêm em PascalCase
 * (`Name`, `State`…) e são lidos sem diferenciar caixa.
 */
export const cityMessages = {
  created: "Cidade criada.",
  updated: "Cidade atualizada.",
  slugTaken: "Esse endereço já existe.",
  slugReserved: "Esse endereço é reservado.",
  notFound: "Essa cidade não existe mais. Recarregue a página.",
} as const;

export const styleMessages = {
  added: "Estilo adicionado.",
  renamed: "Estilo renomeado.",
  taken: "Esse estilo já existe.",
  invalid: "O nome do estilo deve ter de 2 a 30 caracteres.",
  notFound: "Esse estilo não existe mais. Recarregue a página.",
} as const;

/** Erro genérico (sessão expirada, sem permissão, API fora do ar). */
export function genericMessage(status: number): string {
  if (status === 401) {
    return "Sua sessão expirou. Entre de novo para continuar.";
  }
  if (status === 403) {
    return "Você não tem acesso a esta ação.";
  }
  return "Não deu para salvar agora. Tente de novo em instantes.";
}

/** A primeira mensagem do campo `field` (sem diferenciar caixa) num erro de validação. */
export function fieldMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
  field: string,
): string | undefined {
  const entry = Object.entries(result.fields ?? {}).find(
    ([name]) => name.toLowerCase() === field.toLowerCase(),
  );
  return entry?.[1]?.[0];
}
