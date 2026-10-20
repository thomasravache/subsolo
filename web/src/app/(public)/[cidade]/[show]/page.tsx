import type { Metadata } from "next";
import { cache } from "react";

import { getAttendanceCounts } from "@/api/attendances-data";
import { listCities } from "@/api/cities";
import { getShowDetail, type ShowDetailResult } from "@/api/show-detail";
import { ShowPage } from "@/components/shows/ShowPage";
import { showMetadata, showUrl } from "@/components/shows/show-metadata";

import { publicUrl } from "./public-url";
import { ShowNotFound } from "./show-not-found";
import { showStatus } from "./show-status";
import { ShowUnavailable } from "./show-unavailable";

/**
 * SPEC-0024 — página do show `/{cidade}/{show}`, renderizada no servidor a partir da API
 * (`GET /api/v1/cities/{city}/shows/{slug}`): flyer, data e hora no fuso da cidade, casa, line-up, estilos, preço,
 * "Ingresso" e "Compartilhar", com os metadados Open Graph (imagem `og` absoluta pelo `SUBSOLO_PUBLIC_URL`). Show ou
 * cidade inexistente → "Show não encontrado" (404); API fora do ar → "Show indisponível" (503). O status HTTP vem do
 * `proxy` (ADR-0016), que consulta a API antes da renderização; a página renderiza o aviso sem `notFound()`, para o
 * `<title>` da página valer também no 404. SPEC-0029: o "Vou" e a contagem "{n} vão" (do `Engagement`, sem ela se
 * indisponível).
 */

// Sem shell de carregamento: a página só aparece com o show (ou o aviso) pronto.
export const instant = false;

// Renderizada na requisição (sem o shell genérico da rota), para o status definido pelo `proxy` chegar ao navegador.
export const unstable_paramMatching = { cidade: "blocking", show: "blocking" };

/** O show da requisição (uma consulta só para os metadados e a página). */
const loadShow = cache(
  async (cidade: string, show: string): Promise<ShowDetailResult> => {
    const status = await showStatus();
    return status ? { kind: status } : getShowDetail(cidade, show);
  },
);

/** SPEC-0029 — o show já começou (na hora da requisição): sem o "Vou" e com "{n} foram". */
function hasStarted(startsAt: string): boolean {
  return Date.parse(startsAt) <= Date.now();
}

export async function generateMetadata({
  params,
}: PageProps<"/[cidade]/[show]">): Promise<Metadata> {
  const { cidade, show } = await params;
  const result = await loadShow(cidade, show);
  switch (result.kind) {
    case "ok":
      return showMetadata(result.show, publicUrl());
    case "not-found":
      return {
        title: "Show não encontrado · Subsolo",
        robots: { index: false },
      };
    case "unavailable":
      return { title: "Show indisponível · Subsolo" };
  }
}

export default async function ShowDetailPage({
  params,
}: PageProps<"/[cidade]/[show]">) {
  const { cidade, show } = await params;
  const result = await loadShow(cidade, show);
  if (result.kind === "not-found") {
    const city = (await listCities()).find((item) => item.slug === cidade);
    return <ShowNotFound city={city} />;
  }
  if (result.kind === "unavailable") {
    return <ShowUnavailable />;
  }
  // SPEC-0029: a contagem de "vão" vem do Engagement (§4), pela mesma rota em lote da agenda; sem ela, a página sai
  // sem a contagem.
  const counts = await getAttendanceCounts([result.show.id]);
  return (
    <ShowPage
      show={result.show}
      url={showUrl(result.show, publicUrl())}
      attendingCount={counts?.[result.show.id]}
      past={hasStarted(result.show.startsAt)}
    />
  );
}
