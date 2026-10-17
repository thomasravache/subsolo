import type { APIRequestContext } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { plainSentinel, secretSentinel } from "../auth/environment";

/**
 * SPEC-0016 E2E-04 — nenhuma variável de ambiente chega ao navegador: com valores sentinela no ambiente do contêiner
 * `web` (injetados pelo `tools/compose-up.sh`: `SUBSOLO_E2E_SENTINEL`, uma variável comum, e `SUBSOLO_E2E_SECRET_KEY`,
 * com nome de segredo) e a URL interna da API (`http://api:…`), o HTML de `/`, `/entrar`, `/entrar/confirmar` e
 * `/conta` (sem sessão: redireciona para `/entrar`) e todo script `/_next/static/**` que eles referenciam não contêm
 * nenhum desses valores, nem `Password=` nem `ConnectionStrings`.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const pages = ["/", "/entrar", "/entrar/confirmar?token=e2e04", "/conta"];

/** Valores que nunca podem sair do servidor. */
const forbidden = [
  plainSentinel,
  secretSentinel,
  "http://api:",
  "Password=",
  "ConnectionStrings",
];

/** Scripts próprios do Next.js referenciados pelo HTML (`src` com `/_next/static/`). */
function scriptsOf(html: string): string[] {
  const sources = [...html.matchAll(/<script\b[^>]*\bsrc="([^"]+)"/g)]
    .map((match) => match[1] ?? "")
    .filter((src) => src.includes("/_next/static/"));
  return [...new Set(sources)];
}

function leaks(text: string): string[] {
  return forbidden.filter((value) => text.includes(value));
}

async function fetchText(
  request: APIRequestContext,
  path: string,
): Promise<{ status: number; text: string }> {
  const response = await request.get(path);
  return { status: response.status(), text: await response.text() };
}

test.describe("variáveis de ambiente fora do navegador", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do que o servidor entrega, num projeto só (desktop-chrome)",
    );
  });

  for (const path of pages) {
    test(`SPEC-0016:E2E-04 ${path} e os scripts dele não levam variáveis de ambiente ao navegador`, async ({
      request,
    }) => {
      const page = await fetchText(request, path);
      expect(page.status, `${path} (depois dos redirecionamentos)`).toBe(200);
      expect(leaks(page.text), `HTML de ${path}`).toEqual([]);

      const scripts = scriptsOf(page.text);
      expect(scripts.length, `${path} sem scripts do Next.js`).toBeGreaterThan(
        0,
      );
      for (const script of scripts) {
        const asset = await fetchText(request, script);
        expect(asset.status, script).toBe(200);
        expect(leaks(asset.text), `script ${script} de ${path}`).toEqual([]);
      }
    });
  }
});
