import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0023 — chamadas do navegador ao cadastro de show (enviar o flyer, cadastrar, editar e mudar a situação), pela
 * base comum de `./browser` (mesmo domínio, cookie de sessão HttpOnly, `X-CSRF` em toda mutação).
 */
export type ShowInput = components["schemas"]["ShowRequest"];

export type MyShow = components["schemas"]["MyShowResponse"];

export type ShowForEdit = components["schemas"]["ShowForEditResponse"];

export type ShowStatus = components["schemas"]["ShowStatus"];

export type UploadedFlyer = components["schemas"]["FlyerUploadedResponse"];

/** `POST /api/v1/flyers`: envia o arquivo (campo `file`); 201 novo ou 200 já enviado. */
export function uploadFlyer(file: File): Promise<ApiResult<UploadedFlyer>> {
  return guarded(async () => {
    const body = new FormData();
    body.append("file", file);
    const response = await fetch("/api/v1/flyers", {
      method: "POST",
      body,
      credentials: "same-origin",
      cache: "no-store",
      headers: await csrfHeaders(),
    });
    const content: unknown = await response.json().catch(() => undefined);
    return response.ok
      ? { ok: true, value: content as UploadedFlyer }
      : failure(response.status, content);
  });
}

/** `POST /api/v1/shows`: cadastra o show; devolve o id. */
export function createShow(input: ShowInput): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST("/api/v1/shows", {
      body: input,
      headers: await csrfHeaders(),
    });
    return data
      ? { ok: true, value: data.id }
      : failure(response.status, error);
  });
}

/** `PUT /api/v1/shows/{id}`: salva a edição (o slug não muda). */
export function updateShow(
  id: string,
  input: ShowInput,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.PUT("/api/v1/shows/{id}", {
      params: { path: { id } },
      body: input,
      headers: await csrfHeaders(),
    });
    return data
      ? { ok: true, value: data.id }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/shows/{id}/status`: adia, cancela ou reagenda o show. */
export function changeShowStatus(
  id: string,
  status: ShowStatus,
): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST("/api/v1/shows/{id}/status", {
      params: { path: { id } },
      body: { status },
      headers: await csrfHeaders(),
    });
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
