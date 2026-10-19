import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { cache } from "react";

import type { Style } from "@/api/admin";
import {
  getAgendaStyles,
  getCityAgenda,
  type CityAgendaResult,
} from "@/api/agenda";
import { AgendaFilterBar } from "@/components/agenda/AgendaFilterBar";
import { AgendaProducerCta } from "@/components/agenda/AgendaProducerCta";
import {
  agendaApiQuery,
  dayMonth,
  firstParam,
  hasFilters,
  isChoosingDates,
  localToday,
  readAgendaFilters,
  showCount,
  type AgendaFilters,
  type AgendaSearchParams,
} from "@/components/agenda/agenda-filters";
import { buttonClasses } from "@/components/ui/Button";
import { mainContentId } from "@/components/ui/shell";
import { Card } from "@/components/ui/Card";
import { EmptyState } from "@/components/ui/EmptyState";

import { AgendaUnavailable } from "./agenda-unavailable";
import { agendaStatus } from "./agenda-status";

/**
 * SPEC-0008 — agenda da cidade, renderizada no servidor a partir da API (cliente tipado): título
 * "Agenda {Cidade}", lista de shows ou estado vazio; cidade inexistente → página 404; API fora do ar → mensagem
 * de indisponibilidade (503). O status HTTP vem do `proxy`, que consulta a API antes da renderização.
 * SPEC-0011 — dentro do shell público; shows em `Card` (data no fuso da cidade) e estado vazio em `EmptyState`.
 * SPEC-0025 — filtros na URL (`?periodo=semana|mes`, `?de=&ate=`, `?estilo=death,grind`; inválidos são ignorados),
 * lidos no servidor: a URL filtrada é compartilhável e já vem filtrada. Cabeçalho "PRÓXIMOS 90 DIAS · N SHOWS" ou
 * "dd/MM A dd/MM · N SHOWS" (anunciado em `aria-live`), vazio com filtro ("Nenhum show com esses filtros." e "Limpar
 * filtros") e o bloco do produtor no fim. Telas do Stitch "Agenda Curitiba - Subsolo" e "Agenda Curitiba - Desktop".
 */

// Sem shell de carregamento: a página só aparece com a agenda (ou o aviso) pronta.
export const instant = false;

// Cidade fora do build é renderizada na requisição (sem servir o shell genérico de `/[cidade]`), para o status
// definido pelo `proxy` (404/503) chegar ao navegador; com o shell genérico a resposta sairia sempre 200.
export const unstable_paramMatching = { cidade: "blocking" };

interface AgendaPage {
  result: CityAgendaResult;
  filters: AgendaFilters;
  styles: Style[];
}

/**
 * A agenda da requisição com os filtros (uma consulta só para os metadados e a página). "Esta semana" e "este mês"
 * dependem de "hoje" no fuso da cidade, que vem da própria agenda: nesses casos são duas chamadas à API.
 */
const loadAgenda = cache(
  async (
    cidade: string,
    periodo?: string,
    de?: string,
    ate?: string,
    estilo?: string,
  ): Promise<AgendaPage> => {
    const status = await agendaStatus();
    if (status) {
      return {
        result: { kind: status },
        filters: { period: undefined, styles: [] },
        styles: [],
      };
    }
    const styles = await getAgendaStyles();
    const params: AgendaSearchParams = { periodo, de, ate, estilo };
    const filters = readAgendaFilters(
      params,
      styles.map((style) => style.slug),
    );
    const relative =
      filters.period?.kind === "semana" || filters.period?.kind === "mes";
    let result = await getCityAgenda(
      cidade,
      relative
        ? agendaApiQuery({ ...filters, period: undefined }, "")
        : agendaApiQuery(filters, ""),
    );
    if (relative && result.kind === "ok") {
      const today = localToday(result.agenda.city.timeZone, new Date());
      result = await getCityAgenda(cidade, agendaApiQuery(filters, today));
    }
    return { result, filters, styles };
  },
);

async function load({
  params,
  searchParams,
}: PageProps<"/[cidade]">): Promise<AgendaPage> {
  const [{ cidade }, search] = await Promise.all([params, searchParams]);
  return loadAgenda(
    cidade,
    firstParam(search.periodo),
    firstParam(search.de),
    firstParam(search.ate),
    firstParam(search.estilo),
  );
}

export async function generateMetadata(
  props: PageProps<"/[cidade]">,
): Promise<Metadata> {
  const { result } = await load(props);
  switch (result.kind) {
    case "ok":
      return { title: `Agenda ${result.agenda.city.name} · Subsolo` };
    case "not-found":
      return { title: "Cidade não encontrada · Subsolo" };
    case "unavailable":
      return { title: "Agenda indisponível · Subsolo" };
  }
}

export default async function CityAgendaPage(props: PageProps<"/[cidade]">) {
  const { result, filters, styles } = await load(props);
  if (result.kind === "not-found") {
    notFound();
  }
  if (result.kind === "unavailable") {
    return <AgendaUnavailable />;
  }

  const { city, shows, period, total } = result.agenda;
  const choosing = isChoosingDates(await props.searchParams);
  const summary = filters.period
    ? `${dayMonth(period.from)} A ${dayMonth(period.to)} · ${showCount(total)}`
    : `PRÓXIMOS 90 DIAS · ${showCount(total)}`;
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-5 px-4 py-8"
    >
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-3xl uppercase leading-none tracking-wide text-text md:text-5xl">
          Agenda {city.name}
        </h1>
        <p
          aria-live="polite"
          className="font-label text-xs font-medium uppercase tracking-wider text-text-muted"
        >
          {summary}
        </p>
      </header>
      <div className="flex flex-col gap-5 lg:grid lg:grid-cols-[15rem_minmax(0,1fr)] lg:items-start lg:gap-8">
        <div className="lg:sticky lg:top-20">
          <AgendaFilterBar
            citySlug={city.slug}
            filters={filters}
            styles={styles}
            choosing={choosing}
          />
        </div>
        <div className="flex flex-col gap-6">
          {shows.length > 0 ? (
            <ul className="grid gap-3.5 md:grid-cols-2 xl:grid-cols-3">
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
          ) : hasFilters(filters) ? (
            <div className="flex flex-col items-center gap-3 rounded-md border border-dashed border-border px-4 py-10 text-center">
              <p className="text-lg text-text">
                Nenhum show com esses filtros.
              </p>
              <Link
                href={`/${city.slug}`}
                className={buttonClasses("secondary")}
              >
                Limpar filtros
              </Link>
            </div>
          ) : (
            <EmptyState
              title={`Nenhum show anunciado ainda em ${city.name}.`}
              hint="Os shows aparecem aqui assim que forem anunciados."
            />
          )}
          <AgendaProducerCta cityName={city.name} />
        </div>
      </div>
    </main>
  );
}
