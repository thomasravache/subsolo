import type { components } from "./schema";

/**
 * SPEC-0026 — perfil público de banda, casa ou produtor para as páginas `/banda|casa|produtor/{slug}`, e os shows dele.
 * Esqueleto do Test-writer: o Implementer escreve as chamadas (G2) e troca os tipos escritos à mão pelos gerados do
 * OpenAPI (`pnpm api:generate`).
 */
export type PublicProfileType = "band" | "venue" | "producer";

/** O corpo de `GET /api/v1/profiles/{type}/{slug}` (§6). */
export interface PublicProfile {
  id: string;
  type: PublicProfileType;
  name: string;
  slug: string;
  city: { slug: string; name: string };
  styles: { slug: string; name: string }[];
  links: {
    instagram?: string | null;
    bandcamp?: string | null;
    metalArchives?: string | null;
  };
}

/** Um show da lista do perfil (o resumo da agenda, SPEC-0008). */
export type ProfileShow = components["schemas"]["ShowSummaryResponse"];

/**
 * - `ok`: o perfil aprovado;
 * - `not-found`: inexistente, pendente, recusado ou de outro tipo (404);
 * - `unavailable`: API fora do ar ou com erro 5xx.
 */
export type PublicProfileResult =
  | { kind: "ok"; profile: PublicProfile }
  | { kind: "not-found" }
  | { kind: "unavailable" };

/** `ok` com os shows; `unavailable` quando o endpoint de shows falha (a página mostra o perfil com o aviso). */
export type ProfileShowsResult =
  { kind: "ok"; shows: ProfileShow[] } | { kind: "unavailable" };

export async function getPublicProfile(
  type: PublicProfileType,
  slug: string,
): Promise<PublicProfileResult> {
  throw new Error(
    `SPEC-0026: getPublicProfile(${type}, ${slug}) ainda não implementado.`,
  );
}

export async function getProfileShows(
  profileId: string,
  period: "upcoming" | "past",
): Promise<ProfileShowsResult> {
  throw new Error(
    `SPEC-0026: getProfileShows(${profileId}, ${period}) ainda não implementado.`,
  );
}
