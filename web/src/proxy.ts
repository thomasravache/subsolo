import { NextResponse, type NextRequest } from "next/server";

import { getCityAgenda, type CityAgendaResult } from "@/api/agenda";
import { agendaStatusHeader } from "@/app/(public)/[cidade]/agenda-status";

/**
 * SPEC-0008 — o status HTTP da agenda `/{cidade}` é decidido aqui, antes da renderização (ADR-0016): com Cache
 * Components a página começa a ser enviada (status 200) pelo shell estático antes de os dados da API chegarem.
 * Cidade inexistente ou slug fora do padrão → 404 e API fora do ar → 503. A página é avisada pelo cabeçalho
 * interno {@link agendaStatusHeader}, que o proxy sempre sobrescreve: o valor mandado pelo cliente nunca chega lá.
 */

/** Formato do slug no contrato da API (`^[a-z0-9-]{2,60}$`); fora dele nem se consulta a API. */
const citySlug = /^[a-z0-9-]{2,60}$/;

const statusByKind: Record<CityAgendaResult["kind"], number> = {
  ok: 200,
  "not-found": 404,
  unavailable: 503,
};

export async function proxy(request: NextRequest) {
  const slug = request.nextUrl.pathname.slice(1);
  const kind = citySlug.test(slug)
    ? (await getCityAgenda(slug)).kind
    : "not-found";

  const headers = new Headers(request.headers);
  headers.set(agendaStatusHeader, kind);
  if (kind === "ok") {
    return NextResponse.next({ request: { headers } });
  }
  return NextResponse.rewrite(request.nextUrl, {
    status: statusByKind[kind],
    request: { headers },
  });
}

export const config = {
  // Todo caminho de um segmento (`/{cidade}`), menos os internos do Next.js (`/_next/…`, `/_not-found` etc.) e os
  // arquivos públicos (com ponto, ex.: `/robots.txt`; um slug nunca tem ponto). `/` não passa por aqui. Uma página
  // estática nova de um segmento (ex.: `/sobre`) precisa entrar na exceção.
  matcher: "/((?!_)[^/.]+)",
};
