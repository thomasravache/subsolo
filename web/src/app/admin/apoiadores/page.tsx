import type { Metadata } from "next";

import { getSupporters } from "@/api/supporters";
import {
  AdminForbidden,
  AdminPage,
  AdminUnavailable,
} from "@/components/admin/AdminPage";
import { SupportersPanel } from "@/components/admin/SupportersPanel";

import { adminAccess } from "../admin-access";

/**
 * SPEC-0028 — `/admin/apoiadores` (tela "Admin - Apoiadores" do Stitch): listar, adicionar e remover (com confirmação)
 * os apoiadores da página Apoie. Renderizada por requisição: o `proxy` decide o status (anônimo → 307 para `/entrar`;
 * sem o papel admin → 403; API fora do ar → 503) e serve com `no-store`. As mutações saem do navegador, com o cookie de
 * sessão e o X-CSRF.
 */
export const instant = false;

const path = "/admin/apoiadores";

export async function generateMetadata(): Promise<Metadata> {
  switch (await adminAccess()) {
    case "ok":
      return { title: "Apoiadores | Admin | Subsolo" };
    case "forbidden":
      return { title: "Acesso negado | Subsolo" };
    case "unavailable":
      return { title: "Painel indisponível | Admin | Subsolo" };
  }
}

export default async function SupportersAdminPage() {
  const access = await adminAccess();
  if (access === "forbidden") {
    return <AdminForbidden />;
  }
  const result = access === "ok" ? await getSupporters() : undefined;
  if (result?.kind !== "ok") {
    return <AdminUnavailable />;
  }

  return (
    <AdminPage current={path} title="Apoiadores">
      <SupportersPanel initialSupporters={result.supporters} />
    </AdminPage>
  );
}
