import { browserApi as api, failure, guarded, type ApiResult } from "./browser";
import type { components } from "./schema";

/** SPEC-0026 (H23-08) — um perfil aprovado na busca do cadastro de show (com ou sem dono). */
export type LookupProfile = components["schemas"]["ProfileLookupResponse"];

/**
 * SPEC-0026 (H23-08, Emenda v2 a) — `GET /api/v1/profiles/lookup`: perfis aprovados, com ou sem dono, cujo nome contém
 * `q` (ao menos 2 caracteres), do `type` quando informado (até 20, por nome). Chamada do navegador (artista ou admin),
 * pela base comum de `./browser`.
 */
export function lookupProfiles(
  q: string,
  type: "band" | "venue" | "producer" | undefined,
  signal?: AbortSignal,
): Promise<ApiResult<LookupProfile[]>> {
  return guarded(async () => {
    const { data, response, error } = await api.GET("/api/v1/profiles/lookup", {
      params: { query: type ? { q, type } : { q } },
      cache: "no-store",
      signal,
    });
    return data ? { ok: true, value: data } : failure(response.status, error);
  });
}
