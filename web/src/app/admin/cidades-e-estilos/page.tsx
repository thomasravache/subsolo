import type { Metadata } from "next";

import { getAdminPanelData } from "@/api/admin-data";
import { AdminNav } from "@/components/admin/AdminNav";
import { AdminPanel } from "@/components/admin/AdminPanel";
import { mainContentId } from "@/components/ui/shell";

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

const pageTitle =
  "font-display text-4xl uppercase leading-none tracking-wide text-text md:text-5xl";

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

function Message({ title, text }: { title: string; text: string }) {
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-3xl flex-1 flex-col gap-4 px-4 py-10"
    >
      <h1 className={pageTitle}>{title}</h1>
      <p role="alert" className="text-lg text-text-muted">
        {text}
      </p>
    </main>
  );
}

function Unavailable() {
  return (
    <Message
      title="Painel indisponível"
      text="O painel está fora do ar por alguns instantes. Tente de novo."
    />
  );
}

export default async function CitiesAndStylesPage() {
  const access = await adminAccess();
  if (access === "forbidden") {
    return (
      <Message
        title="Acesso negado"
        text="Você não tem acesso a esta página."
      />
    );
  }
  const data = access === "ok" ? await getAdminPanelData() : undefined;
  if (!data) {
    return <Unavailable />;
  }

  return (
    <div className="mx-auto flex w-full max-w-7xl flex-1 flex-col gap-6 px-4 py-8 md:flex-row md:gap-10">
      <aside className="md:w-56 md:shrink-0">
        <AdminNav current={path} />
      </aside>
      <main id={mainContentId} className="flex min-w-0 flex-1 flex-col gap-6">
        <h1 className={pageTitle}>Cidades e estilos</h1>
        <AdminPanel cities={data.cities} styles={data.styles} />
      </main>
    </div>
  );
}
