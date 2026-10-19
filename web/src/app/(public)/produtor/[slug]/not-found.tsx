import type { Metadata } from "next";

import { ProfileNotFound } from "@/components/profiles/ProfileNotFound";

/**
 * SPEC-0026 — com o status 404 (definido pelo `proxy`), o Next.js monta os metadados com os do `not-found` mais
 * próximo, não com os da página (como na página do show). A página renderiza o aviso ela mesma; este componente só vale
 * para um `notFound()`.
 */
export const metadata: Metadata = { title: "Perfil não encontrado · Subsolo" };

export default function ProfileNotFoundBoundary() {
  return <ProfileNotFound />;
}
