import type { PublicProfileType } from "@/api/public-profile";

/**
 * SPEC-0026 — as páginas de perfil e o tipo da API de cada uma: `/banda/{slug}` → `band`, `/casa/{slug}` → `venue`,
 * `/produtor/{slug}` → `producer` (o tipo da URL tem de bater com o do perfil, senão 404).
 */
export const profileTypeBySegment: Record<string, PublicProfileType> = {
  banda: "band",
  casa: "venue",
  produtor: "producer",
};

/** Caminho de uma página de perfil: `/{banda|casa|produtor}/{slug}`. */
export const profilePath = /^\/(banda|casa|produtor)\/([^/.]+)$/;

/** Formato do slug do perfil (SPEC-0021: até 80 caracteres); fora dele nem se consulta a API. */
export const profileSlug = /^[a-z0-9-]{1,80}$/;
