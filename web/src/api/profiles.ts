import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0021 — chamadas do navegador à API de perfis (pedir, buscar e reivindicar; e, no admin, aprovar, recusar e
 * criar perfil sem dono), pela base comum de `./browser` (mesmo domínio, cookie de sessão HttpOnly, `X-CSRF` em toda
 * mutação).
 */
export type ProfileType = "band" | "venue" | "producer";

export type ProfileLinks = components["schemas"]["ProfileLinksRequest"];

export type ClaimableProfile =
  components["schemas"]["ClaimableProfileResponse"];

export type MyProfiles = components["schemas"]["MyProfilesResponse"];

export type QueueItem = components["schemas"]["ProfileRequestQueueItem"];

/** Os campos do formulário de perfil (pedido do fã ou perfil sem dono do admin). */
export interface ProfileInput {
  type: ProfileType | null;
  name: string;
  city: string;
  styles: string[];
  links: ProfileLinks;
}

/** `POST /api/v1/profiles/requests`: pede o perfil, com o contato para a aprovação. */
export function requestProfile(
  input: ProfileInput,
  contact: string,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST(
      "/api/v1/profiles/requests",
      { body: { ...input, contact }, headers: await csrfHeaders() },
    );
    return data
      ? { ok: true, value: data.requestId }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/profiles`: o admin cria o perfil aprovado e sem dono. */
export function createOwnerlessProfile(
  input: ProfileInput,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST("/api/v1/admin/profiles", {
      body: input,
      headers: await csrfHeaders(),
    });
    return data
      ? { ok: true, value: data.profileId }
      : failure(response.status, error);
  });
}

/** `GET /api/v1/profiles/search`: perfis aprovados sem dono cujo nome contém `q` (até 20). */
export function searchClaimableProfiles(
  q: string,
  signal?: AbortSignal,
): Promise<ApiResult<ClaimableProfile[]>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/profiles/search", {
      params: { query: { q } },
      cache: "no-store",
      signal,
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** `POST /api/v1/profiles/{id}/claims`: reivindica o perfil sem dono, com a prova. */
export function claimProfile(
  profileId: string,
  proof: string,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST(
      "/api/v1/profiles/{id}/claims",
      {
        params: { path: { id: profileId } },
        body: { proof },
        headers: await csrfHeaders(),
      },
    );
    return data
      ? { ok: true, value: data.requestId }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/profile-requests/{id}/approve`: aprova o pedido. */
export function approveProfileRequest(
  requestId: string,
): Promise<ApiResult<string>> {
  return guarded(async () => {
    const { data, response, error } = await api.POST(
      "/api/v1/admin/profile-requests/{id}/approve",
      { params: { path: { id: requestId } }, headers: await csrfHeaders() },
    );
    return data
      ? { ok: true, value: data.profileId }
      : failure(response.status, error);
  });
}

/** `POST /api/v1/admin/profile-requests/{id}/reject`: recusa o pedido com o motivo. */
export function rejectProfileRequest(
  requestId: string,
  reason: string,
): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await api.POST(
      "/api/v1/admin/profile-requests/{id}/reject",
      {
        params: { path: { id: requestId } },
        body: { reason },
        headers: await csrfHeaders(),
      },
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
