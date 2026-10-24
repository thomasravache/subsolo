import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0033 — chamadas do navegador à API de denúncias (§6), pela base comum de `./browser` (mesmo domínio, cookie de
 * sessão HttpOnly, `X-CSRF` em toda mutação): denunciar o show (`POST /api/v1/shows/{id}/reports`) e as decisões do
 * admin (`POST /api/v1/admin/reports/{showId}/keep` e `/hide`). A fila é lida no servidor (`./reports-data`).
 */
export type ReportReason = keyof components["schemas"]["ReportCountsResponse"];

export interface ReportInput {
  reason: ReportReason;
  details: string | null;
}

/** Um show na fila de denúncias (`GET /api/v1/admin/reports`). */
export type ReportQueueItem = components["schemas"]["ReportQueueItem"];

/** `POST /api/v1/shows/{id}/reports`: denuncia o show; devolve o id da denúncia. */
export function reportShow(
  showId: string,
  input: ReportInput,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST(
      "/api/v1/shows/{id}/reports",
      {
        params: { path: { id: showId } },
        body: input,
        headers: await csrfHeaders(),
      },
    );
    return data
      ? { ok: true, value: data.id }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/reports/{showId}/keep`: mantém o show e arquiva as denúncias. */
export function keepReportedShow(showId: string): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST(
      "/api/v1/admin/reports/{showId}/keep",
      { params: { path: { showId } }, headers: await csrfHeaders() },
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/reports/{showId}/hide`: oculta o show (com o motivo interno opcional). */
export function hideReportedShow(
  showId: string,
  note: string | null,
): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST(
      "/api/v1/admin/reports/{showId}/hide",
      {
        params: { path: { showId } },
        body: { note },
        headers: await csrfHeaders(),
      },
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}

/** A sessão de quem toca em "Reportar" (`GET /api/v1/me`): logado → `ok`; anônimo → 401. */
export function getReporterSession(): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.GET("/api/v1/me", {
      cache: "no-store",
    });
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
