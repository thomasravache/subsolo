import type { Metadata } from "next";
import { notFound } from "next/navigation";

import { getCityAgenda, type CityAgendaResult } from "@/api/agenda";

import { AgendaUnavailable } from "./agenda-unavailable";
import { agendaStatus } from "./agenda-status";

/**
 * SPEC-0008 — agenda da cidade, renderizada no servidor a partir da API (cliente tipado): título
 * "Agenda {Cidade}", lista de shows ou estado vazio; cidade inexistente → página 404; API fora do ar → mensagem
 * de indisponibilidade (503). O status HTTP vem do `proxy`, que consulta a API antes da renderização.
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
      return { title: "Cidade não encontrada · Subsolo" };
    case "unavailable":
      return { title: "Agenda indisponível · Subsolo" };
  }
}

export default async function CityAgendaPage({
  params,
}: PageProps<"/[cidade]">) {
  const result = await loadAgenda(params);
  if (result.kind === "not-found") {
    notFound();
  }
  if (result.kind === "unavailable") {
    return <AgendaUnavailable />;
  }

  const { city, shows } = result.agenda;
  return (
    <main className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-6 px-4 py-10">
      <h1 className="text-3xl font-semibold tracking-tight">
        Agenda {city.name}
      </h1>
      {shows.length === 0 ? (
        <p className="text-lg text-foreground/80">
          Nenhum show anunciado ainda em {city.name}.
        </p>
      ) : (
        <ul className="flex flex-col gap-4">
          {shows.map((show) => (
            <li key={show.id}>{show.title}</li>
          ))}
        </ul>
      )}
    </main>
  );
}
