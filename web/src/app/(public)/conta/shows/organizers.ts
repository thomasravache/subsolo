import { getMyProfiles } from "@/api/profiles-data";
import type { OrganizerOption } from "@/components/shows/ShowForm";

/**
 * SPEC-0023 — os perfis do usuário que podem organizar um show (para o admin, os dele; os demais vêm da busca de perfis,
 * que acha qualquer perfil aprovado — SPEC-0026, H23-08).
 */
export async function getOrganizers(): Promise<OrganizerOption[] | undefined> {
  const mine = await getMyProfiles();
  return mine?.profiles.map((profile) => ({
    id: profile.id,
    name: profile.name,
    city: profile.city,
  }));
}
