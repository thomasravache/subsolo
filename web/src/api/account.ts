import createClient from "openapi-fetch";

import type { paths } from "./schema";

/**
 * SPEC-0016 — chamadas do navegador à API da conta (entrar por link mágico, sair, quem sou eu). O navegador fala com
 * a API pelo mesmo domínio (`/api/...`, o Caddy encaminha), com o cookie de sessão `__Host-subsolo` (HttpOnly) indo e
 * voltando sozinho: nenhum endereço vem do ambiente e nada da sessão fica no JavaScript. Toda mutação leva o token
 * anti-forgery no cabeçalho `X-CSRF` (SPEC-0010), pedido logo antes.
 */
const api = createClient<paths>({ baseUrl: "", credentials: "same-origin" });

const antiforgeryPath = "/api/v1/antiforgery";

/** Resultado de uma chamada: sucesso ou o código de erro estável do ProblemDetails (`extensions.code`). */
export type AccountResult<T = undefined> =
  { ok: true; value: T } | { ok: false; code: string; status: number };

/** Código usado quando a API não responde (rede fora, tempo esgotado) ou responde sem ProblemDetails. */
export const unavailableCode = "network.unavailable";

/** Cabeçalhos com o token anti-forgery novo, para a mutação seguinte. */
async function csrfHeaders(): Promise<Record<string, string>> {
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

function failure(status: number, error: unknown): AccountResult<never> {
  const code =
    typeof error === "object" && error !== null && "code" in error
      ? String((error as { code: unknown }).code)
      : unavailableCode;
  return { ok: false, code, status };
}

async function guarded<T>(
  call: () => Promise<AccountResult<T>>,
): Promise<AccountResult<T>> {
  try {
    return await call();
  } catch {
    return { ok: false, code: unavailableCode, status: 0 };
  }
}

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
