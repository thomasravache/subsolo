import { NextResponse, type NextRequest } from "next/server";

import { getCityAgenda } from "@/api/agenda";
import { agendaStatusHeader } from "@/app/(public)/[cidade]/agenda-status";

/**
 * SPEC-0008 — o status HTTP da agenda `/{cidade}` é decidido aqui, antes da renderização: com Cache Components a
 * página começa a ser enviada (status 200) pelo shell estático antes de os dados da API chegarem. Cidade
 * inexistente → 404 e API fora do ar → 503, com a página avisada pelo cabeçalho {@link agendaStatusHeader}.
 */
export async function proxy(request: NextRequest) {
  const slug = request.nextUrl.pathname.slice(1);
  const result = await getCityAgenda(slug);
  if (result.kind === "ok") {
    return NextResponse.next();
  }

  const headers = new Headers(request.headers);
  headers.set(agendaStatusHeader, result.kind);
  return NextResponse.rewrite(request.nextUrl, {
    status: result.kind === "not-found" ? 404 : 503,
    request: { headers },
  });
}

export const config = {
  // Só a agenda (`/{cidade}`, slug válido); `/` e os arquivos do Next.js não passam por aqui.
  matcher: "/:cidade([a-z0-9-]{2,60})",
};
