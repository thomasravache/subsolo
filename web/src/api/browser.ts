import createClient from "openapi-fetch";

import type { paths } from "./schema";

/**
 * SPEC-0016/SPEC-0017 — base das chamadas do navegador à API: mesmo domínio (`/api/...`, o Caddy encaminha), com o
 * cookie de sessão `__Host-subsolo` (HttpOnly) indo e voltando sozinho — nenhum endereço vem do ambiente e nada da
 * sessão fica no JavaScript. Toda mutação leva o token anti-forgery no cabeçalho `X-CSRF` (SPEC-0010), pedido logo
 * antes ({@link csrfHeaders}).
 */
export const browserApi = createClient<paths>({
  baseUrl: "",
  credentials: "same-origin",
});

const antiforgeryPath = "/api/v1/antiforgery";

/** Resultado de uma chamada: sucesso ou o código de erro estável do ProblemDetails (`extensions.code`). */
export type ApiResult<T = undefined> =
  | { ok: true; value: T }
  | {
      ok: false;
      code: string;
      status: number;
      /** Campos inválidos (`errors` do ProblemDetails de validação), com as mensagens. */
      fields?: Record<string, string[]>;
    };

/** Código usado quando a API não responde (rede fora, tempo esgotado) ou responde sem ProblemDetails. */
export const unavailableCode = "network.unavailable";

/** Cabeçalhos com o token anti-forgery novo, para a mutação seguinte. */
export async function csrfHeaders(): Promise<Record<string, string>> {
  const response = await fetch(antiforgeryPath, {
    credentials: "same-origin",
    cache: "no-store",
  });
  if (!response.ok) {
    throw new Error(`antiforgery: ${response.status}`);
  }
  const { token } = (await response.json()) as { token: string };
  return { "X-CSRF": token };
}

/** Falha a partir do ProblemDetails (código e, se houver, os campos inválidos). */
export function failure(status: number, error: unknown): ApiResult<never> {
  const problem =
    typeof error === "object" && error !== null
      ? (error as { code?: unknown; errors?: unknown })
      : {};
  const code = "code" in problem ? String(problem.code) : unavailableCode;
  const fields =
    typeof problem.errors === "object" && problem.errors !== null
      ? (problem.errors as Record<string, string[]>)
      : undefined;
  return fields
    ? { ok: false, code, status, fields }
    : { ok: false, code, status };
}

/** Rede fora ou resposta inesperada → {@link unavailableCode}. */
export async function guarded<T>(
  call: () => Promise<ApiResult<T>>,
): Promise<ApiResult<T>> {
  try {
    return await call();
  } catch {
    return { ok: false, code: unavailableCode, status: 0 };
  }
}
