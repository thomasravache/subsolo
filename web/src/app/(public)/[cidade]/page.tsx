import type { Metadata } from "next";

import { getCityAgenda, type CityAgendaResult } from "@/api/agenda";
import { mainContentId } from "@/components/ui/shell";
import { Card } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";

import { AgendaUnavailable } from "./agenda-unavailable";
import { agendaStatus } from "./agenda-status";
import CityNotFound from "./not-found";

/**
 * SPEC-0008 — agenda da cidade, renderizada no servidor a partir da API (cliente tipado): título
 * "Agenda {Cidade}", lista de shows ou estado vazio; cidade inexistente → página 404; API fora do ar → mensagem
 * de indisponibilidade (503). O status HTTP vem do `proxy`, que consulta a API antes da renderização.
 * SPEC-0024 (CH-01, achado da SPEC-0015) — a cidade inexistente é renderizada pela própria página (sem `notFound()`,
 * com o 404 já definido pelo `proxy`), para o `<title>` "Cidade não encontrada · Subsolo" valer também na página
 * carregada direto (com `notFound()` saía o título do layout raiz).
 * SPEC-0011 — dentro do shell público; shows em `Card` (data no fuso da cidade) e estado vazio em `EmptyState`.
 */

// Sem shell de carregamento: a página só aparece com a agenda (ou o aviso) pronta.
export const instant = false;

// Cidade fora do build é renderizada na requisição (sem servir o shell genérico de `/[cidade]`), para o status
// definido pelo `proxy` (404/503) chegar ao navegador; com o shell genérico a resposta sairia sempre 200.
export const unstable_paramMatching = { cidade: "blocking" };

async function loadAgenda(
  params: PageProps<"/[cidade]">["params"],
): Promise<CityAgendaResult> {
  const status = await agendaStatus();
  if (status) {
    return { kind: status };
  }
  const { cidade } = await params;
  return getCityAgenda(cidade);
}

export async function generateMetadata({
  params,
}: PageProps<"/[cidade]">): Promise<Metadata> {
  const result = await loadAgenda(params);
  switch (result.kind) {
    case "ok":
      return { title: `Agenda ${result.agenda.city.name} · Subsolo` };
    case "not-found":
      return {
        title: "Cidade não encontrada · Subsolo",
        robots: { index: false },
      };
    case "unavailable":
      return { title: "Agenda indisponível · Subsolo" };
  }
}

export default async function CityAgendaPage({
  params,
}: PageProps<"/[cidade]">) {
  const result = await loadAgenda(params);
  if (result.kind === "not-found") {
    return <CityNotFound />;
  }
  if (result.kind === "unavailable") {
    return <AgendaUnavailable />;
  }

  const { city, shows } = result.agenda;
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-6 px-4 py-8"
    >
      <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
        Agenda {city.name}
      </h1>
      {shows.length === 0 ? (
        <EmptyState
          title={`Nenhum show anunciado ainda em ${city.name}.`}
          hint="Os shows aparecem aqui assim que forem anunciados."
        />
      ) : (
        <ul className="grid gap-3.5 md:grid-cols-2 lg:grid-cols-3">
          {shows.map((show) => (
            <li key={show.id}>
              <Card
                href={`/${city.slug}/${show.slug}`}
                flyerSrc={show.flyerThumbUrl}
                title={show.title}
                date={show.startsAt}
                timeZone={city.timeZone}
                venue={show.venueName}
                lineup={show.lineup}
                styles={show.styles}
                status={show.status}
                attendingCount={show.attendingCount}
              />
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
