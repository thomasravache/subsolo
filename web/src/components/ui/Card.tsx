import type { components } from "@/api/schema";

/** Situação do show no contrato `ShowSummary` da API. */
export type ShowStatus = components["schemas"]["ShowStatus"];

/**
 * Card de show da agenda, alimentado pelos campos do `ShowSummary` da API.
 * - `href`: página do show;
 * - `flyerSrc`: miniatura do flyer (`flyerThumbUrl`); sem flyer, placeholder escuro com o nome do show;
 * - `date`: início do show em ISO 8601 (`startsAt`), exibido no fuso `timeZone` (IANA) da cidade;
 * - `status`: `cancelled`/`postponed` mostram o `Badge` "Cancelado"/"Adiado" com papel de status.
 */
export interface CardProps {
  href: string;
  flyerSrc?: string | null;
  title: string;
  date: string;
  timeZone: string;
  venue: string;
  lineup: string[];
  styles: string[];
  status: ShowStatus;
  attendingCount: number;
}

// Scaffold: sem implementação.
export function Card(props: CardProps) {
  void props;
  return null;
}
