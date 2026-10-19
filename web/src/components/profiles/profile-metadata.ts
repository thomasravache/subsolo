import type { Metadata } from "next";

import type { ProfileShow, PublicProfile } from "@/api/public-profile";

/**
 * SPEC-0026 — os metadados da página do perfil (§6): `<title>` "{nome} · {Banda|Casa|Produtor} · Subsolo",
 * `description` "{tipo} de {cidade} · {estilos}" e Open Graph com o flyer `og` do próximo show (ou o padrão do site).
 * Esqueleto do Test-writer: o Implementer escreve a montagem (G2).
 */
export function profileMetadata(
  profile: PublicProfile,
  nextShow: ProfileShow | undefined,
  publicUrl: string,
): Metadata {
  throw new Error(
    `SPEC-0026: profileMetadata(${profile.slug}, ${nextShow?.slug ?? "-"}, ${publicUrl}) ainda não implementado.`,
  );
}
