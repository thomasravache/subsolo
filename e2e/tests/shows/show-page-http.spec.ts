import { expect, test } from "../fixtures";
import { appURL, externalApp, webWithoutApiURL } from "../agenda/environment";
import { parseCsp, scriptNonce } from "../security/csp";
import { canonicalOf, meta, publishShows, titleOf } from "./show-page";

/**
 * SPEC-0024 — o HTML da página do show baixado sem JavaScript (como um robô de prévia do WhatsApp, do Instagram ou do
 * Telegram), na pilha do Compose:
 * - IT-02: `og:image` absoluta (pelo `App:PublicUrl`, aqui o endereço da pilha) para a variante `og` do flyer, com
 *   `og:image:width` 1200 e `og:image:height` 630, `og:title`, `og:description`, `og:url`, `og:type` website,
 *   `twitter:card` summary_large_image e `link rel=canonical`; a imagem responde 200 JPEG;
 * - IT-03: slug inexistente e cidade inexistente → 404 real com "Show não encontrado"; um show com `<script>` no título
 *   sai com o texto escapado e a CSP com nonce (os scripts da página com o nonce da resposta);
 * - IT-04: o Next.js de produção sem API (o mesmo do IT-04 da SPEC-0008) responde a página do show com 503.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("página do show sem JavaScript", () => {
  test("SPEC-0024:IT-02 o HTML tem os metadados Open Graph com a imagem og absoluta 1200×630", async ({
    request,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
    test.setTimeout(300_000);
    const { producer, shows } = await publishShows(
      browser,
      "og01",
      (suffix) => [{ title: `Noite da Peste ${suffix}` }],
    );
    const [show] = shows;
    if (!show) {
      throw new Error("preparação sem o show");
    }

    const response = await request.get(show.path, {
      headers: { "user-agent": "WhatsApp/2.23.20.0" },
    });
    expect(response.status()).toBe(200);
    const html = await response.text();

    const origin = new URL(appURL).origin;
    const canonical = `${origin}${show.path}`;
    const image = meta(html, "og:image");
    expect(image, "og:image").toBe(
      `${origin}/media/flyers/${show.flyerId}-og.jpg`,
    );
    expect(meta(html, "og:image:width")).toBe("1200");
    expect(meta(html, "og:image:height")).toBe("630");
    expect(meta(html, "og:title") ?? "").toContain(show.title);
    expect(meta(html, "og:description") ?? "").toContain(show.venue);
    expect(meta(html, "og:url")).toBe(canonical);
    expect(meta(html, "og:type")).toBe("website");
    expect(meta(html, "twitter:card")).toBe("summary_large_image");
    expect(canonicalOf(html), "link rel=canonical").toBe(canonical);
    expect(titleOf(html)).toBe(
      `${show.title} · ${producer.city.name} · Subsolo`,
    );

    const og = await request.get(image ?? "");
    expect(og.status(), `GET ${image}`).toBe(200);
    expect(og.headers()["content-type"]).toBe("image/jpeg");
  });

  test("SPEC-0024:IT-03 show ou cidade inexistente dá 404 real com Show não encontrado", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    for (const path of [
      "/cidade-exemplo/show-que-nao-existe-2026-01-01",
      "/cidade-que-nao-existe/show-que-nao-existe-2026-01-01",
    ]) {
      const response = await request.get(path);
      expect(response.status(), `GET ${path}`).toBe(404);
      expect(
        (await response.text()).includes("Show não encontrado"),
        `GET ${path} sem "Show não encontrado"`,
      ).toBe(true);
    }
  });

  test("SPEC-0024:IT-03 título com <script> sai escapado e a página tem a CSP com nonce", async ({
    request,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
    test.setTimeout(300_000);
    const injected = '<script>alert("peste")</script>';
    const { shows } = await publishShows(browser, "xss01", (suffix) => [
      { title: `${injected} Noite ${suffix}` },
    ]);
    const [show] = shows;
    if (!show) {
      throw new Error("preparação sem o show");
    }

    const response = await request.get(show.path);
    expect(response.status()).toBe(200);
    const html = await response.text();

    expect(
      html.includes(injected),
      "o <script> do título foi para o HTML sem escape",
    ).toBe(false);
    expect(
      html.includes("&lt;script&gt;"),
      "o título escapado não está na página",
    ).toBe(true);
    const nonce = scriptNonce(
      parseCsp(response.headers()["content-security-policy"]),
    );
    expect(nonce, "CSP sem nonce no script-src").toBeTruthy();
    const scripts = [...html.matchAll(/<script\b([^>]*)>/gi)].map(
      (match) => match[1] ?? "",
    );
    expect(scripts.length, "página sem scripts").toBeGreaterThan(0);
    for (const attributes of scripts) {
      expect(attributes, "script sem o nonce da resposta").toContain(
        `nonce="${nonce}"`,
      );
    }
  });

  test("SPEC-0024:IT-04 sem API, a página do show responde 503 com a página de erro", async ({
    request,
  }, testInfo) => {
    test.skip(
      externalApp,
      "com BASE_URL o webServer não sobe o Next.js sem API",
    );
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    const response = await request.get(
      `${webWithoutApiURL}/cidade-exemplo/noite-da-peste-ii-2026-01-01`,
    );

    expect(response.status()).toBe(503);
    const html = await response.text();
    expect(/<h1\b/i.test(html), "a página de erro sem <h1>").toBe(true);
    expect(
      html.includes("Show não encontrado"),
      "o 503 não pode ser a página de show inexistente",
    ).toBe(false);
  });
});
