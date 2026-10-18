import type { Metadata } from "next";

import { getProfileFormData, getProfileQueue } from "@/api/profiles-data";
import {
  AdminForbidden,
  AdminPage,
  AdminUnavailable,
} from "@/components/admin/AdminPage";
import { ProfileRequestsQueue } from "@/components/admin/ProfileRequestsQueue";

import { adminAccess } from "../admin-access";

/**
 * SPEC-0021 — `/admin/fila` (tela "Fila do Admin - Subsolo" do Stitch, só com a aba "Pedidos de perfil"; a de denúncias
 * entra com a spec dela): os pedidos de perfil e as reivindicações pendentes, com "Aprovar" e "Recusar". O `proxy`
 * decide o status (anônimo → 307 para `/entrar`, sem o papel admin → 403, API fora do ar → 503) e serve com
 * `no-store`; as decisões saem do navegador, com o cookie de sessão e o X-CSRF.
 */
export const instant = false;

const path = "/admin/fila";

export async function generateMetadata(): Promise<Metadata> {
  switch (await adminAccess()) {
    case "ok":
      return { title: "Fila | Admin | Subsolo" };
    case "forbidden":
      return { title: "Acesso negado | Subsolo" };
    case "unavailable":
      return { title: "Painel indisponível | Admin | Subsolo" };
  }
}

export default async function QueuePage() {
  const access = await adminAccess();
  if (access === "forbidden") {
    return <AdminForbidden />;
  }
  const [queue, form] =
    access === "ok"
      ? await Promise.all([getProfileQueue(), getProfileFormData()])
      : [undefined, undefined];
  if (!queue || !form) {
    return <AdminUnavailable />;
  }

  return (
    <AdminPage current={path} title="Fila do admin">
      <ProfileRequestsQueue initialItems={queue} cities={form.cities} />
    </AdminPage>
  );
}
