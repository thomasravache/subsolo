import type { Metadata } from "next";

import { getAdminPanelData } from "@/api/admin-data";
import {
  AdminForbidden,
  AdminPage,
  AdminUnavailable,
} from "@/components/admin/AdminPage";
import { AdminPanel } from "@/components/admin/AdminPanel";

import { adminAccess } from "../admin-access";

/**
 * SPEC-0017 — `/admin/cidades-e-estilos` (tela "Admin - Cidades e Estilos" do Stitch, com os ajustes aprovados: sem
 * textos de exclusão nem "Regra da cena", sem contagens de shows, menu só com o que existe, endereço só leitura na
 * edição e o domínio real). Renderizada por requisição: o `proxy` decide o status (anônimo → 307 para `/entrar`; sem
 * o papel admin → 403 com "Você não tem acesso a esta página."; API fora do ar → 503) e serve com `no-store`. As
 * mutações saem do navegador, com o cookie de sessão e o X-CSRF.
 */
export const instant = false;

const path = "/admin/cidades-e-estilos";

export async function generateMetadata(): Promise<Metadata> {
  switch (await adminAccess()) {
    case "ok":
      return { title: "Cidades e estilos | Admin | Subsolo" };
    case "forbidden":
      return { title: "Acesso negado | Subsolo" };
    case "unavailable":
      return { title: "Painel indisponível | Admin | Subsolo" };
  }
}

export default async function CitiesAndStylesPage() {
  const access = await adminAccess();
  if (access === "forbidden") {
    return <AdminForbidden />;
  }
  const data = access === "ok" ? await getAdminPanelData() : undefined;
  if (!data) {
    return <AdminUnavailable />;
  }

  return (
    <AdminPage current={path} title="Cidades e estilos">
      <AdminPanel cities={data.cities} styles={data.styles} />
    </AdminPage>
  );
}
