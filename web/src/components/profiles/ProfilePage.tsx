import type { ReactNode } from "react";

import type { ProfileShow, PublicProfile } from "@/api/public-profile";

/**
 * SPEC-0026 — a página pública do perfil (§6): nome, cidade, estilos, links das redes, "PRÓXIMOS SHOWS" e "SHOWS
 * PASSADOS". `upcoming`/`past` = `null` quando o endpoint de shows falhou ("Não foi possível carregar os shows agora.").
 * Esqueleto do Test-writer: o Implementer escreve a página (G2).
 */
export interface ProfilePageProps {
  profile: PublicProfile;
  upcoming: ProfileShow[] | null;
  past: ProfileShow[] | null;
}

export function ProfilePage({ profile }: ProfilePageProps): ReactNode {
  throw new Error(
    `SPEC-0026: ProfilePage(${profile.slug}) ainda não implementado.`,
  );
}
