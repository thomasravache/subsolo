/**
 * SPEC-0026 (H24-06) — o único lugar de `components/shows` que monta o formatador de data e hora no fuso da cidade:
 * a página do show, os metadados, "Meus shows" e a página do perfil pedem as partes aqui.
 */
export interface CityTimeParts {
  /** Dia da semana abreviado, sem ponto ("sáb"). */
  weekday: string;
  /** Dia com dois dígitos ("14"). */
  day: string;
  /** Mês abreviado, sem ponto ("nov"). */
  month: string;
  /** Mês com dois dígitos ("11"). */
  monthNumber: string;
  /** Ano com quatro dígitos. */
  year: string;
  /** Hora no relógio de 24 h, com dois dígitos ("21"). */
  hour: string;
  /** Minuto com dois dígitos ("00"). */
  minute: string;
}

/** As partes de `iso` no fuso `timeZone` (IANA) da cidade, em pt-BR. */
export function cityTimeParts(iso: string, timeZone: string): CityTimeParts {
  const date = new Date(iso);
  const named = partsOf(date, timeZone, "short");
  const numeric = partsOf(date, timeZone, "2-digit");
  return {
    weekday: named("weekday"),
    day: named("day"),
    month: named("month"),
    monthNumber: numeric("month"),
    year: named("year"),
    hour: named("hour"),
    minute: named("minute"),
  };
}

/** Leitor das partes de `date` no fuso, com o mês por extenso abreviado ou em dois dígitos (sem os pontos). */
function partsOf(
  date: Date,
  timeZone: string,
  month: "short" | "2-digit",
): (type: Intl.DateTimeFormatPartTypes) => string {
  const parts = new Intl.DateTimeFormat("pt-BR", {
    timeZone,
    weekday: "short",
    day: "2-digit",
    month,
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date);
  return (type) =>
    (parts.find((item) => item.type === type)?.value ?? "").replace(".", "");
}
