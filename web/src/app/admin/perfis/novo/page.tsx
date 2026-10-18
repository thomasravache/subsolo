import type { Metadata } from "next";

import { getProfileFormData } from "@/api/profiles-data";
import {
  AdminForbidden,
  AdminPage,
  AdminUnavailable,
} from "@/components/admin/AdminPage";
import { ProfileForm } from "@/components/profiles/ProfileForm";

import { adminAccess } from "../../admin-access";

/**
 * SPEC-0021 — `/admin/perfis/novo`: o admin cria um perfil aprovado e sem dono (o formulário de "Pedir perfil" sem o
 * contato), para cadastrar shows enquanto a banda, a casa ou o produtor ainda não está no Subsolo; "Criar perfil" →
 * "Perfil criado.". Status decidido pelo `proxy`, como as outras páginas do admin.
 */
export const instant = false;

const path = "/admin/perfis/novo";

export async function generateMetadata(): Promise<Metadata> {
  switch (await adminAccess()) {
    case "ok":
      return { title: "Novo perfil | Admin | Subsolo" };
    case "forbidden":
      return { title: "Acesso negado | Subsolo" };
    case "unavailable":
      return { title: "Painel indisponível | Admin | Subsolo" };
  }
}

export default async function NewOwnerlessProfilePage() {
  const access = await adminAccess();
  if (access === "forbidden") {
    return <AdminForbidden />;
  }
  const data = access === "ok" ? await getProfileFormData() : undefined;
  if (!data) {
    return <AdminUnavailable />;
  }

  return (
    <AdminPage current={path} title="Novo perfil sem dono">
      <p className="max-w-xl text-base text-text-muted">
        O perfil nasce aprovado e sem dono: os shows podem ser cadastrados nele
        e o dono pode reivindicá-lo depois.
      </p>
      <div className="max-w-xl">
        <ProfileForm
          mode="ownerless"
          cities={data.cities}
          styles={data.styles}
        />
      </div>
    </AdminPage>
  );
}
