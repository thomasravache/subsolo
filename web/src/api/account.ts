import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";

export { unavailableCode } from "./browser";

/**
 * SPEC-0016 — chamadas do navegador à API da conta (entrar por link mágico, sair, quem sou eu), pela base comum de
 * `./browser` (mesmo domínio, cookie de sessão HttpOnly, `X-CSRF` em toda mutação).
 */

/** Resultado de uma chamada: sucesso ou o código de erro estável do ProblemDetails (`extensions.code`). */
export type AccountResult<T = undefined> = ApiResult<T>;

/** `POST /api/v1/auth/magic-link`: pede o link de acesso para o e-mail (202 igual exista a conta ou não). */
export function requestMagicLink(
  email: string,
  returnTo: string | null,
): Promise<AccountResult> {
  return guarded(async () => {
    const { response, error } = await api.POST("/api/v1/auth/magic-link", {
      body: { email, returnTo },
      headers: await csrfHeaders(),
    });
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/auth/magic-link/consume`: troca o token do link pela sessão; devolve o caminho de volta. */
export function consumeMagicLink(
  token: string,
): Promise<AccountResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST(
      "/api/v1/auth/magic-link/consume",
      { body: { token }, headers: await csrfHeaders() },
    );
    return data
      ? { ok: true, value: data.returnTo }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/auth/logout`: revoga a sessão no servidor e apaga o cookie. */
export function signOut(): Promise<AccountResult> {
  return guarded(async () => {
    const { response, error } = await api.POST("/api/v1/auth/logout", {
      headers: await csrfHeaders(),
    });
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}

/** `GET /api/v1/me` pelo navegador: há sessão válida no cookie? (para a navegação "Entrar"/"Conta"). */
export async function hasSession(signal?: AbortSignal): Promise<boolean> {
  try {
    const { response } = await api.GET("/api/v1/me", {
      cache: "no-store",
      signal,
    });
    return response.ok;
  } catch {
    return false;
  }
}
