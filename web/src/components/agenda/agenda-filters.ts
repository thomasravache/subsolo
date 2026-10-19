/**
 * SPEC-0025 — filtros da agenda da cidade (`/{cidade}?periodo=&de=&ate=&estilo=`, §6), lidos da URL no servidor:
 * - `periodo=semana` → de hoje até o próximo domingo; `periodo=mes` → de hoje até o último dia do mês (no fuso da
 *   cidade);
 * - `de=aaaa-mm-dd&ate=aaaa-mm-dd` → o intervalo escolhido (`ate ≥ de`, no máximo {@link maxPeriodDays} dias);
 * - `estilo=death,grind` → slugs normalizados (minúsculas, sem vazios nem repetidos, só os estilos conhecidos);
 * - parâmetro inválido → ignorado (agenda padrão), sem erro na página.
 *
 * Scaffold do Test-writer (fase Red): as assinaturas estão definidas; o comportamento vem na fase Green.
 */

/** Duração máxima de um intervalo escolhido (a mesma da API). */
export const maxPeriodDays = 180;

/** Os parâmetros de busca da página (`searchParams` do Next.js). */
export type AgendaSearchParams = Record<string, string | string[] | undefined>;

/** O período pedido na URL. */
export type AgendaPeriodFilter =
  | { kind: "semana" }
  | { kind: "mes" }
  | { kind: "intervalo"; from: string; to: string };

/** Os filtros válidos da URL; `period` ausente = agenda padrão (próximos 90 dias). */
export interface AgendaFilters {
  period: AgendaPeriodFilter | undefined;
  styles: string[];
}

/** O período em datas locais da cidade (`aaaa-mm-dd`). */
export interface AgendaDateRange {
  from: string;
  to: string;
}

/** Os parâmetros de `GET /api/v1/cities/{slug}/agenda` (só os que existirem). */
export interface AgendaApiQuery {
  from?: string;
  to?: string;
  styles?: string;
}

/** A data local (`aaaa-mm-dd`) de `now` no fuso `timeZone` (IANA). */
export function localToday(timeZone: string, now: Date): string {
  void timeZone;
  void now;
  throw new Error("SPEC-0025: localToday ainda não implementado");
}

/** Lê os filtros da URL; `knownStyles` são os slugs de `GET /api/v1/styles`. */
export function readAgendaFilters(
  searchParams: AgendaSearchParams,
  knownStyles: readonly string[],
): AgendaFilters {
  void searchParams;
  void knownStyles;
  throw new Error("SPEC-0025: readAgendaFilters ainda não implementado");
}

/** O intervalo de datas do período, a partir de `today` (`aaaa-mm-dd`, já no fuso da cidade). */
export function resolvePeriod(
  period: AgendaPeriodFilter,
  today: string,
): AgendaDateRange {
  void period;
  void today;
  throw new Error("SPEC-0025: resolvePeriod ainda não implementado");
}

/** Os parâmetros da API para os filtros (sem período → sem `from`/`to`; sem estilos → sem `styles`). */
export function agendaApiQuery(
  filters: AgendaFilters,
  today: string,
): AgendaApiQuery {
  void filters;
  void today;
  throw new Error("SPEC-0025: agendaApiQuery ainda não implementado");
}
