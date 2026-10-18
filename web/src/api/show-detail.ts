/**
 * SPEC-0024 (andaime do §6) — o corpo de `GET /api/v1/cities/{city}/shows/{slug}`, para a página do show e os
 * metadados Open Graph. Escrito à mão a partir do contrato; quando o snapshot do OpenAPI tiver a rota, vira um apelido
 * do tipo gerado em `./schema` (`components["schemas"][…]`).
 */
export type ShowDetailStatus = "scheduled" | "cancelled" | "postponed";

export interface ShowDetailPart {
  name: string;
  profileSlug: string | null;
}

export interface ShowDetail {
  id: string;
  slug: string;
  title: string;
  city: { slug: string; name: string; timeZone: string };
  /** Início em UTC (ISO 8601). */
  startsAt: string;
  venue: ShowDetailPart;
  lineup: ShowDetailPart[];
  styles: { slug: string; name: string }[];
  price: string;
  ticketUrl: string | null;
  status: ShowDetailStatus;
  flyer: {
    thumbUrl: string;
    fullUrl: string;
    ogUrl: string;
    width: number;
    height: number;
  };
}
