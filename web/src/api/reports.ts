import type { ApiResult } from "./browser";

/**
 * SPEC-0033 — scaffold das chamadas de denúncia (§6) feitas pelo navegador: denunciar o show
 * (`POST /api/v1/shows/{id}/reports`) e as decisões do admin (`POST /api/v1/admin/reports/{showId}/keep` e `/hide`).
 * Os tipos são provisórios: com o OpenAPI regenerado, passam a vir de `components["schemas"]`. Implementação pendente.
 */
export type ReportReason =
  "fake" | "cancelled_unannounced" | "wrong_info" | "other";

export interface ReportInput {
  reason: ReportReason;
  details: string | null;
}

/** Um show na fila de denúncias (`GET /api/v1/admin/reports`). */
export interface ReportQueueItem {
  showId: string;
  title: string;
  citySlug: string;
  slug: string;
  startsAtUtc: string;
  venueName: string;
  counts: Record<ReportReason, number>;
  latestDetails: string[];
  firstReportedAtUtc: string;
}

/** `POST /api/v1/shows/{id}/reports`: denuncia o show; devolve o id da denúncia. */
export function reportShow(
  showId: string,
  input: ReportInput,
): Promise<ApiResult<string>> {
  return Promise.reject(
    new Error(`SPEC-0033: reportShow(${showId}, ${input.reason})`),
  );
}

/** `POST /api/v1/admin/reports/{showId}/keep`: mantém o show e arquiva as denúncias. */
export function keepReportedShow(showId: string): Promise<ApiResult> {
  return Promise.reject(new Error(`SPEC-0033: keepReportedShow(${showId})`));
}

/** `POST /api/v1/admin/reports/{showId}/hide`: oculta o show (com o motivo interno opcional). */
export function hideReportedShow(
  showId: string,
  note: string | null,
): Promise<ApiResult> {
  return Promise.reject(
    new Error(`SPEC-0033: hideReportedShow(${showId}, ${note ?? ""})`),
  );
}
