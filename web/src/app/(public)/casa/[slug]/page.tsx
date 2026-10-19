import type { Metadata } from "next";

import {
  ProfileRoute,
  profileRouteMetadata,
} from "@/components/profiles/profile-route";

/**
 * SPEC-0026 — página pública `/casa/{slug}` (perfil do tipo `venue`), renderizada no servidor a partir da API: nome,
 * cidade, estilos, links das redes, próximos shows e passados. Perfil inexistente, pendente, recusado ou de outro tipo →
 * "Perfil não encontrado" (404); API de perfis fora → "Perfil indisponível" (503). O status HTTP vem do `proxy`
 * (ADR-0016).
 */

// Sem shell de carregamento: a página só aparece com o perfil (ou o aviso) pronto.
export const instant = false;

// Renderizada na requisição (sem o shell genérico da rota), para o status definido pelo `proxy` chegar ao navegador.
export const unstable_paramMatching = { slug: "blocking" };

export async function generateMetadata({
  params,
}: PageProps<"/casa/[slug]">): Promise<Metadata> {
  const { slug } = await params;
  return profileRouteMetadata("venue", slug);
}

export default async function ProfileDetailPage({
  params,
}: PageProps<"/casa/[slug]">) {
  const { slug } = await params;
  return <ProfileRoute type="venue" slug={slug} />;
}
