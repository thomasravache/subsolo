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
 * `./browser` (mesmo domínio, cookie de sessão HttpOnly, `X-CSRF` em toda mutação). SPEC-0019 — "Minha conta": mudar
 * o nome, baixar os dados e excluir a conta.
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

/** SPEC-0019 — `PUT /api/v1/me`: muda o nome de exibição (vazio remove); devolve o nome gravado. */
export function updateDisplayName(
  displayName: string,
): Promise<AccountResult<string | null>> {
  return guarded(async () => {
    const { data, response, error } = await api.PUT("/api/v1/me", {
      body: { displayName },
      headers: await csrfHeaders(),
    });
    return data
      ? { ok: true, value: data.displayName ?? null }
      : failure(response.status, error);
  });
}

/** SPEC-0019 — `GET /api/v1/me/export`: o arquivo com os dados do usuário (para o navegador baixar). */
export function exportPersonalData(): Promise<AccountResult<Blob>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/me/export", {
      cache: "no-store",
      parseAs: "blob",
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** SPEC-0019 — `DELETE /api/v1/me`: exclui a conta com o e-mail digitado; a API apaga o cookie de sessão. */
export function deleteAccount(confirmEmail: string): Promise<AccountResult> {
  return guarded(async () => {
    const { response, error } = await api.DELETE("/api/v1/me", {
      body: { confirmEmail },
      headers: await csrfHeaders(),
    });
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
