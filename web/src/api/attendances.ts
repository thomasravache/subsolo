import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0029 — chamadas do navegador à API do "vou", pela base comum de `./browser` (mesmo domínio, cookie de sessão
 * HttpOnly, `X-CSRF` em toda mutação): os shows em que o fã vai (`GET /api/v1/me/attendances?period=upcoming`),
 * marcar (`PUT`) e desmarcar (`DELETE`) o "vou" num show.
 */
export type MyAttendances = components["schemas"]["MyAttendancesResponse"];

/** `GET /api/v1/me/attendances?period=upcoming` (sem sessão: `auth.required`, status 401). */
export function getMyAttendances(
  signal?: AbortSignal,
): Promise<ApiResult<MyAttendances>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/me/attendances", {
      params: { query: { period: "upcoming" } },
      cache: "no-store",
      signal,
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** Marcar (`PUT`) ou desmarcar (`DELETE`) o "vou" no show; 204 nos dois. */
export function setAttending(
  showId: string,
  attending: boolean,
): Promise<ApiResult> {
  return guarded(async () => {
    const path = "/api/v1/me/attendances/{showId}";
    const init = {
      params: { path: { showId } },
      headers: await csrfHeaders(),
    };
    const { response, error } = attending
      ? await api.PUT(path, init)
      : await api.DELETE(path, init);
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
