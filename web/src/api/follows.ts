import {
  browserApi as api,
  csrfHeaders,
  failure,
  guarded,
  type ApiResult,
} from "./browser";
import type { components } from "./schema";

/**
 * SPEC-0027 — chamadas do navegador à API de seguir, pela base comum de `./browser` (mesmo domínio, cookie de sessão
 * HttpOnly, `X-CSRF` em toda mutação): o que o fã segue (`GET /api/v1/me/follows`), seguir (`PUT`) e deixar de seguir
 * (`DELETE`) uma cidade ou um perfil.
 */
export type MyFollows = components["schemas"]["MyFollowsResponse"];

export type FollowedCity = components["schemas"]["FollowedCityResponse"];

export type FollowedProfile = components["schemas"]["FollowedProfileResponse"];

/** O alvo de um follow: a cidade pelo slug ou o perfil (banda, casa, produtor) pelo id. */
export type FollowTarget =
  { kind: "city"; slug: string } | { kind: "profile"; id: string };

/** O alvo está em `follows`? */
export function isFollowing(follows: MyFollows, target: FollowTarget): boolean {
  return target.kind === "city"
    ? follows.cities.some((city) => city.slug === target.slug)
    : follows.profiles.some((profile) => profile.id === target.id);
}

/** `GET /api/v1/me/follows` (sem sessão: `auth.required`, status 401). */
export function getMyFollows(
  signal?: AbortSignal,
): Promise<ApiResult<MyFollows>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/me/follows", {
      cache: "no-store",
      signal,
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}

/** A chamada de seguir (`PUT`) ou deixar de seguir (`DELETE`) do alvo. */
async function send(
  target: FollowTarget,
  following: boolean,
  headers: Record<string, string>,
): Promise<{ response: Response; error?: unknown }> {
  if (target.kind === "city") {
    const path = "/api/v1/me/follows/cities/{slug}";
    const init = { params: { path: { slug: target.slug } }, headers };
    return following ? api.PUT(path, init) : api.DELETE(path, init);
  }
  const path = "/api/v1/me/follows/profiles/{profileId}";
  const init = { params: { path: { profileId: target.id } }, headers };
  return following ? api.PUT(path, init) : api.DELETE(path, init);
}

/** Seguir (`PUT`) ou deixar de seguir (`DELETE`) o alvo; 204 nos dois. */
export function setFollowing(
  target: FollowTarget,
  following: boolean,
): Promise<ApiResult> {
  return guarded(async () => {
    const { response, error } = await send(
      target,
      following,
      await csrfHeaders(),
    );
    return response.ok
      ? { ok: true, value: undefined }
      : failure(response.status, error);
  });
}
