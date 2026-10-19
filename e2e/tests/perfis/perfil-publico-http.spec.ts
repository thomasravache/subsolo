import { expect, test } from "../fixtures";
import { appURL, externalApp, webWithoutApiURL } from "../agenda/environment";
import { meta, titleOf } from "../shows/show-page";
import { publishProfiles } from "./perfil-publico";

/**
 * SPEC-0026 — o HTML das páginas de perfil baixado sem JavaScript (como um robô de prévia), na pilha do Compose:
 * - IT-03: `/banda/{slug}` tem o `<title>` "{nome} · Banda · Subsolo", a `description` "Banda de {cidade} · {estilos}",
 *   `og:title`, `og:description` e `og:image` absoluta da variante `og` do flyer do próximo show; os links das redes no
 *   HTML com `rel="noopener noreferrer"`; a casa sem estilos tem "Casa de {cidade}"; o perfil sem shows não tem imagem
 *   de flyer;
 * - IT-04: `/casa/{slug-de-banda}` (tipo errado) e `/banda/inexistente` → 404 real com "Perfil não encontrado".
 * - IT-05 (Emenda v2, d): o Next.js de produção sem API (o mesmo do IT-04 da SPEC-0008) responde a página do perfil com
 *   503 e a página "Perfil indisponível", com a mensagem em `role="alert"` (nunca "Perfil não encontrado").
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test.describe("página do perfil sem JavaScript", () => {
  test("SPEC-0026:IT-03 o HTML tem o título, a descrição e o Open Graph do §6", async ({
    request,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
    test.setTimeout(300_000);
    const { producer, band, venue, bandWithoutShows, show } =
      await publishProfiles(browser, "perfil03");
    const origin = new URL(appURL).origin;

    const response = await request.get(band.path, {
      headers: { "user-agent": "WhatsApp/2.23.20.0" },
    });
    expect(response.status(), `GET ${band.path}`).toBe(200);
    const html = await response.text();
    expect(titleOf(html)).toBe(`${band.name} · Banda · Subsolo`);
    const description = meta(html, "description") ?? "";
    expect(description.startsWith(`Banda de ${producer.city.name} · `)).toBe(
      true,
    );
    expect(description).toContain("Death");
    expect(meta(html, "og:title") ?? "").toContain(band.name);
    expect(meta(html, "og:description")).toBe(description);
    expect(meta(html, "og:image")).toBe(
      `${origin}/media/flyers/${show.flyerId}-og.jpg`,
    );
    for (const href of [band.instagram, band.bandcamp, band.metalArchives]) {
      const anchor = [...html.matchAll(/<a\b([^>]*)>/gi)]
        .map((match) => match[1] ?? "")
        .find((attributes) => attributes.includes(`href="${href}"`));
      expect(anchor, `link ${href} ausente no HTML`).toBeDefined();
      expect(anchor ?? "").toMatch(/rel="[^"]*noopener[^"]*"/);
      expect(anchor ?? "").toMatch(/rel="[^"]*noreferrer[^"]*"/);
    }

    const venueHtml = await (await request.get(venue.path)).text();
    expect(titleOf(venueHtml)).toBe(`${venue.name} · Casa · Subsolo`);
    expect(meta(venueHtml, "description")).toBe(
      `Casa de ${producer.city.name}`,
    );

    const quietHtml = await (await request.get(bandWithoutShows.path)).text();
    expect(meta(quietHtml, "og:image") ?? "").not.toContain("/media/flyers/");
  });

  test("SPEC-0026:IT-04 perfil do tipo errado ou inexistente dá 404 real com Perfil não encontrado", async ({
    request,
    browser,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
    test.setTimeout(300_000);
    const { band } = await publishProfiles(browser, "perfil04");

    for (const path of [`/casa/${band.slug}`, "/banda/inexistente"]) {
      const response = await request.get(path);
      expect(response.status(), `GET ${path}`).toBe(404);
      expect(
        (await response.text()).includes("Perfil não encontrado"),
        `GET ${path} sem "Perfil não encontrado"`,
      ).toBe(true);
    }
  });

  test("SPEC-0026:IT-05 sem a API de perfis, a página do perfil responde 503 com Perfil indisponível", async ({
    page,
  }, testInfo) => {
    test.skip(
      externalApp,
      "com BASE_URL o webServer não sobe o Next.js sem API",
    );
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    for (const path of ["/banda/podridao", "/casa/porao-do-rock"]) {
      const response = await page.goto(`${webWithoutApiURL}${path}`);

      expect(response?.status(), `GET ${path} sem API`).toBe(503);
      await expect(
        page.getByRole("heading", {
          level: 1,
          name: /perfil indispon[íi]vel/i,
        }),
      ).toBeVisible();
      await expect(page.getByRole("alert").first()).toBeVisible();
      await expect(page.getByText("Perfil não encontrado")).toHaveCount(0);
    }
  });
});
