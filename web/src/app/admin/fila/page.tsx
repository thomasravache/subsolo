import type { Metadata } from "next";

import { getProfileFormData, getProfileQueue } from "@/api/profiles-data";
import { getReportQueue } from "@/api/reports-data";
import { AdminQueueTabs } from "@/components/admin/AdminQueueTabs";
import {
  AdminForbidden,
  AdminPage,
  AdminUnavailable,
} from "@/components/admin/AdminPage";

import { adminAccess } from "../admin-access";

/**
 * SPEC-0021 — `/admin/fila` (tela "Fila do Admin - Subsolo" do Stitch): os pedidos de perfil e as reivindicações
 * pendentes, com "Aprovar" e "Recusar". SPEC-0033: a aba "Denúncias (n)", com os shows denunciados, "Manter" e "Ocultar
 * show" (a fila sai de `GET /api/v1/admin/reports`, `no-store`; SPEC-0037: se essa leitura falhar, só a aba de denúncias
 * avisa). O `proxy`
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
  const [queue, form, reports] =
    access === "ok"
      ? await Promise.all([
          getProfileQueue(),
          getProfileFormData(),
          getReportQueue(),
        ])
      : [undefined, undefined, undefined];
  // SPEC-0037 (H33-03): sem a fila de denúncias, a página segue com os pedidos de perfil e só o painel de denúncias
  // avisa; sem os pedidos de perfil (ou sem o acesso), o painel inteiro fica indisponível.
  if (!queue || !form) {
    return <AdminUnavailable />;
  }

  return (
    <AdminPage current={path} title="Fila do admin">
      <AdminQueueTabs
        profileRequests={queue}
        cities={form.cities}
        reports={reports}
      />
    </AdminPage>
  );
}
