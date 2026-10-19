/**
 * SPEC-0025 — filtros da agenda da cidade (`/{cidade}?periodo=&de=&ate=&estilo=`, §6), lidos da URL no servidor:
 * - `periodo=semana` → de hoje até o próximo domingo; `periodo=mes` → de hoje até o último dia do mês (no fuso da
 *   cidade);
 * - `de=aaaa-mm-dd&ate=aaaa-mm-dd` → o intervalo escolhido (`ate ≥ de`, no máximo {@link maxPeriodDays} dias);
 * - `estilo=death,grind` → slugs normalizados (minúsculas, sem vazios nem repetidos, só os estilos conhecidos);
 * - parâmetro inválido → ignorado (agenda padrão), sem erro na página.
 * Com `periodo=semana|mes`, o período vale mais que `de`/`ate`. `periodo=escolher` só abre o formulário de datas
 * ("Escolher data"), sem filtrar.
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
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(now);
  const part = (type: Intl.DateTimeFormatPartTypes) =>
    parts.find((item) => item.type === type)?.value ?? "";
  return `${part("year")}-${part("month")}-${part("day")}`;
}

const isoDate = /^(\d{4})-(\d{2})-(\d{2})$/;
const dayMs = 86_400_000;

/** O dia (em ms UTC, meia-noite) de uma data `aaaa-mm-dd` que existe; senão `undefined`. */
function dayOf(value: string | undefined): number | undefined {
  const match = value ? isoDate.exec(value) : null;
  if (!match) {
    return undefined;
  }
  const [year, month, day] = match.slice(1).map(Number) as [
    number,
    number,
    number,
  ];
  const time = Date.UTC(year, month - 1, day);
  const date = new Date(time);
  return date.getUTCFullYear() === year &&
    date.getUTCMonth() === month - 1 &&
    date.getUTCDate() === day
    ? time
    : undefined;
}

function isoOf(time: number): string {
  return new Date(time).toISOString().slice(0, 10);
}

/** O primeiro valor de um parâmetro de busca. */
export function firstParam(
  value: string | string[] | undefined,
): string | undefined {
  return Array.isArray(value) ? value[0] : value;
}

/** Lê os filtros da URL; `knownStyles` são os slugs de `GET /api/v1/styles`. */
export function readAgendaFilters(
  searchParams: AgendaSearchParams,
  knownStyles: readonly string[],
): AgendaFilters {
  return {
    period: readPeriod(searchParams),
    styles: readStyles(firstParam(searchParams.estilo), knownStyles),
  };
}

function readPeriod(
  searchParams: AgendaSearchParams,
): AgendaPeriodFilter | undefined {
  const periodo = firstParam(searchParams.periodo);
  if (periodo === "semana" || periodo === "mes") {
    return { kind: periodo };
  }
  const from = firstParam(searchParams.de);
  const to = firstParam(searchParams.ate);
  const fromDay = dayOf(from);
  const toDay = dayOf(to);
  if (
    from === undefined ||
    to === undefined ||
    fromDay === undefined ||
    toDay === undefined ||
    toDay < fromDay ||
    (toDay - fromDay) / dayMs > maxPeriodDays
  ) {
    return undefined;
  }
  return { kind: "intervalo", from, to };
}

function readStyles(
  value: string | undefined,
  knownStyles: readonly string[],
): string[] {
  const known = new Set(knownStyles);
  const styles = (value ?? "")
    .split(",")
    .map((style) => style.trim().toLowerCase())
    .filter((style) => known.has(style));
  return [...new Set(styles)];
}

/** `periodo=escolher`: o formulário "Escolher data" aberto. */
export function isChoosingDates(searchParams: AgendaSearchParams): boolean {
  return firstParam(searchParams.periodo) === "escolher";
}

/** O intervalo de datas do período, a partir de `today` (`aaaa-mm-dd`, já no fuso da cidade). */
export function resolvePeriod(
  period: AgendaPeriodFilter,
  today: string,
): AgendaDateRange {
  if (period.kind === "intervalo") {
    return { from: period.from, to: period.to };
  }
  const day = dayOf(today);
  if (day === undefined) {
    throw new Error(`SPEC-0025: data de hoje inválida: ${today}`);
  }
  const date = new Date(day);
  if (period.kind === "semana") {
    const untilSunday = (7 - date.getUTCDay()) % 7;
    return { from: today, to: isoOf(day + untilSunday * dayMs) };
  }
  return {
    from: today,
    to: isoOf(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)),
  };
}

/** Os parâmetros da API para os filtros (sem período → sem `from`/`to`; sem estilos → sem `styles`). */
export function agendaApiQuery(
  filters: AgendaFilters,
  today: string,
): AgendaApiQuery {
  const query: AgendaApiQuery = {};
  if (filters.period) {
    const range = resolvePeriod(filters.period, today);
    query.from = range.from;
    query.to = range.to;
  }
  if (filters.styles.length > 0) {
    query.styles = filters.styles.join(",");
  }
  return query;
}

/** Se há algum filtro (período ou estilo) na URL. */
export function hasFilters(filters: AgendaFilters): boolean {
  return filters.period !== undefined || filters.styles.length > 0;
}

/**
 * O endereço da agenda com os filtros (`?periodo=` ou `?de=&ate=`, e `?estilo=`); `choosing` abre o formulário de
 * datas. Sem filtros, `/{cidade}`.
 */
export function agendaHref(
  citySlug: string,
  filters: AgendaFilters,
  choosing = false,
): string {
  const params: string[] = [];
  if (choosing) {
    params.push("periodo=escolher");
  } else if (filters.period?.kind === "intervalo") {
    params.push(`de=${filters.period.from}`, `ate=${filters.period.to}`);
  } else if (filters.period) {
    params.push(`periodo=${filters.period.kind}`);
  }
  if (filters.styles.length > 0) {
    params.push(`estilo=${filters.styles.map(encodeURIComponent).join(",")}`);
  }
  const path = `/${encodeURIComponent(citySlug)}`;
  return params.length > 0 ? `${path}?${params.join("&")}` : path;
}

/** Uma data `aaaa-mm-dd` como `dd/MM` (cabeçalho da agenda). */
export function dayMonth(date: string): string {
  const [, month, day] = date.split("-");
  return `${day}/${month}`;
}

/** "N SHOWS" (singular "1 SHOW"). */
export function showCount(total: number): string {
  return total === 1 ? "1 SHOW" : `${total} SHOWS`;
}
