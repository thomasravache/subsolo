import { NextResponse, type NextRequest } from "next/server";

import { getCityAgenda, type CityAgendaResult } from "@/api/agenda";
import { sessionForCookie } from "@/api/session";
import { agendaStatusHeader } from "@/app/(public)/[cidade]/agenda-status";
import { adminAccessHeader, type AdminAccess } from "@/app/admin/admin-access";
import { isAccountPage, isAdminPage } from "@/components/ui/shell";

/**
 * SPEC-0010 — toda página passa por aqui: o proxy gera um nonce novo por requisição, monta a Content-Security-Policy
 * com ele e a repassa ao Next.js no cabeçalho da requisição (o Next.js extrai o nonce e o aplica nos próprios
 * scripts das páginas renderizadas por requisição, ADR-0017); a resposta leva a CSP e os demais cabeçalhos de
 * segurança.
 *
 * SPEC-0008 — o status HTTP da agenda `/{cidade}` é decidido aqui, antes da renderização (ADR-0016): com Cache
 * Components a página começa a ser enviada (status 200) pelo shell estático antes de os dados da API chegarem.
 * Cidade inexistente ou slug fora do padrão → 404 e API fora do ar → 503. A página é avisada pelo cabeçalho
 * interno {@link agendaStatusHeader}, que o proxy sempre sobrescreve: o valor mandado pelo cliente nunca chega lá.
 *
 * SPEC-0016 — as páginas de entrar e da conta (`/entrar`, `/entrar/confirmar`, `/conta`) são exceções explícitas
 * (ADR-0016): não são cidades e seguem direto para o Next.js, com `Cache-Control: no-store` (dados de quem está
 * logado, nunca guardados pelo navegador nem por intermediários).
 *
 * SPEC-0017 — as páginas do admin (`/admin/…`) também têm o status decidido aqui, com a sessão de `GET /api/v1/me`
 * (cookie da requisição repassado): anônimo → 307 para `/entrar?returnTo=…`; logado sem o papel admin → 403; API fora
 * do ar → 503; admin → segue. Sempre com `Cache-Control: no-store` e sem consultar a agenda. A página é avisada pelo
 * cabeçalho interno {@link adminAccessHeader}, que o proxy sempre sobrescreve.
 */

/** Formato do slug no contrato da API (`^[a-z0-9-]{2,60}$`); fora dele nem se consulta a API. */
const citySlug = /^[a-z0-9-]{2,60}$/;

/**
 * Caminho da agenda: um segmento (`/{cidade}`), menos os internos do Next.js (`/_next/…`, `/_not-found` etc.); os
 * arquivos públicos (com ponto, ex.: `/robots.txt`; um slug nunca tem ponto) nem passam pelo proxy. Uma página
 * nova de um segmento (ex.: `/sobre`) precisa entrar na exceção, como as de entrar e da conta ({@link isAccountPage}).
 */
const agendaPath = /^\/(?!_)[^/.]+$/;

const statusByKind: Record<CityAgendaResult["kind"], number> = {
  ok: 200,
  "not-found": 404,
  unavailable: 503,
};

/** Cabeçalhos de segurança fixos de toda página (a CSP, com nonce, é montada por requisição). */
const securityHeaders: Record<string, string> = {
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "strict-origin-when-cross-origin",
  "Permissions-Policy": "camera=(), microphone=(), geolocation=()",
};

/** Nonce aleatório de 128 bits em base64, novo a cada requisição. */
function createNonce(): string {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  return btoa(String.fromCharCode(...bytes));
}

/**
 * CSP estrita com nonce: só rodam scripts com o nonce da requisição (`'strict-dynamic'` estende a confiança aos que
 * eles carregam). Em desenvolvimento o React precisa de `'unsafe-eval'` para reconstruir as pilhas de erro.
 */
function contentSecurityPolicy(nonce: string): string {
  const development = process.env.NODE_ENV === "development";
  return [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${development ? " 'unsafe-eval'" : ""}`,
    `style-src 'self' 'nonce-${nonce}'`,
    "img-src 'self' data: blob:",
    "font-src 'self'",
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ].join("; ");
}

const statusByAdminAccess: Record<AdminAccess, number> = {
  ok: 200,
  forbidden: 403,
  unavailable: 503,
};

/** Acesso ao painel do admin pela sessão do cookie recebido; `anonymous` = sem sessão válida (vai para `/entrar`). */
async function adminAccessOf(
  request: NextRequest,
): Promise<AdminAccess | "anonymous"> {
  const session = await sessionForCookie(request.headers.get("cookie") ?? "");
  switch (session.kind) {
    case "signed-in":
      return session.me.roles.includes("admin") ? "ok" : "forbidden";
    case "anonymous":
      return "anonymous";
    default:
      return "unavailable";
  }
}

/** A resposta de uma página do admin, conforme o acesso (o 403 e o 503 renderizam a própria página, com a mensagem). */
async function adminResponse(
  request: NextRequest,
  headers: Headers,
): Promise<NextResponse> {
  const access = await adminAccessOf(request);
  if (access === "anonymous") {
    const signIn = new URL("/entrar", request.nextUrl);
    signIn.searchParams.set("returnTo", request.nextUrl.pathname);
    return NextResponse.redirect(signIn, 307);
  }
  headers.set(adminAccessHeader, access);
  return access === "ok"
    ? NextResponse.next({ request: { headers } })
    : NextResponse.rewrite(request.nextUrl, {
        status: statusByAdminAccess[access],
        request: { headers },
      });
}

async function agendaKind(pathname: string): Promise<CityAgendaResult["kind"]> {
  const slug = pathname.slice(1);
  return citySlug.test(slug) ? (await getCityAgenda(slug)).kind : "not-found";
}

export async function proxy(request: NextRequest) {
  const csp = contentSecurityPolicy(createNonce());
  const headers = new Headers(request.headers);
  headers.set("Content-Security-Policy", csp);

  headers.delete(adminAccessHeader);

  const { pathname } = request.nextUrl;
  const accountPage = isAccountPage(pathname);
  const adminPage = isAdminPage(pathname);
  const kind =
    !accountPage && !adminPage && agendaPath.test(pathname)
      ? await agendaKind(pathname)
      : undefined;
  if (kind) {
    headers.set(agendaStatusHeader, kind);
  } else {
    headers.delete(agendaStatusHeader);
  }

  const response = adminPage
    ? await adminResponse(request, headers)
    : kind === undefined || kind === "ok"
      ? NextResponse.next({ request: { headers } })
      : NextResponse.rewrite(request.nextUrl, {
          status: statusByKind[kind],
          request: { headers },
        });

  response.headers.set("Content-Security-Policy", csp);
  for (const [name, value] of Object.entries(securityHeaders)) {
    response.headers.set(name, value);
  }
  if (accountPage || adminPage) {
    response.headers.set("Cache-Control", "no-store");
  }
  return response;
}

export const config = {
  // Toda página, menos os arquivos internos do Next.js (`/_next/…`) e os arquivos públicos (com ponto no caminho,
  // ex.: `/robots.txt`), que não precisam de CSP. A agenda (`/{cidade}`) é reconhecida por `agendaPath`.
  matcher: "/((?!_next/)(?!.*\\.).*)",
};
