// @vitest-environment jsdom
import { fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * SPEC-0028 — a página `/apoie` (SSR), renderizada com a configuração do servidor e a API simuladas:
 * - UT-01 (parte da página): com `Support:PixKey` (no ambiente do servidor, `Support__PixKey`, com `Support__PixName` e
 *   `Support__PixCity`) a página mostra o QR code (SVG gerado no servidor, sem endereço externo, com o texto
 *   alternativo "QR code do Pix do Subsolo" e conteúdo que muda com a chave), a chave em texto e "Copiar chave", que
 *   copia a chave e anuncia "Chave copiada."; sem a chave, "O Pix do Subsolo chega em breve." no lugar do QR code;
 * - IT-01 (parte da página): a lista "APOIADORES" com os nomes na ordem da API, com link só para quem tem;
 *   vazia → "Seja o primeiro a apoiar.";
 * - IT-03: com a API fora do ar (conexão recusada), a página sai com o Pix e "A lista de apoiadores volta já.".
 *
 * Os textos podem estar em caixa alta só pelo CSS: as buscas não diferenciam maiúsculas onde o §6 usa caixa alta.
 */
const testKey = "chave-teste@subsolo.test";

const supporters = [
  {
    id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
    name: "Putridearth Zine",
    url: "https://zine.example",
  },
  {
    id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f43",
    name: "Abismo Records",
    url: null,
  },
];

function pathOf(input: Parameters<typeof fetch>[0]): string {
  return new URL(
    typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url,
  ).pathname;
}

/** `GET /api/v1/supporters` responde com `list` (ou a API está fora do ar); o resto da API também está fora. */
function stubApi(list: typeof supporters | "down") {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
    if (pathOf(input) === "/api/v1/supporters" && list !== "down") {
      return Response.json(list);
    }
    throw new TypeError("fetch failed");
  });
}

function configurePix(key = testKey) {
  vi.stubEnv("Support__PixKey", key);
  vi.stubEnv("Support__PixName", "Subsolo");
  vi.stubEnv("Support__PixCity", "Curitiba");
}

async function renderPage() {
  const { default: ApoiePage } = await import("./page");
  return render(await ApoiePage());
}

const qrName = "QR code do Pix do Subsolo";

beforeEach(() => {
  vi.stubEnv("API_URL", "http://api.test");
  vi.stubEnv("Support__PixKey", "");
  vi.stubEnv("Support__PixName", "");
  vi.stubEnv("Support__PixCity", "");
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("página Apoie", () => {
  it("SPEC-0028:UT-01 com a chave configurada mostra o texto, o QR code e a chave", async () => {
    configurePix();
    stubApi(supporters);

    await renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: /^apoie o subsolo$/i }),
    ).toBeTruthy();
    expect(
      screen.getByText(
        "O Subsolo é feito pela cena e não tem anúncios. Se ele te ajuda a achar shows, apoie com um Pix de qualquer valor.",
      ),
    ).toBeTruthy();
    const qr = screen.getByRole("img", { name: qrName });
    // SVG gerado no servidor: inline ou em data URI, nunca de um endereço externo (CSP img-src 'self' data:).
    const source = qr.getAttribute("src");
    expect(
      qr.tagName.toLowerCase() === "svg" ||
        (source ?? "").startsWith("data:image/svg+xml"),
      `QR code não é SVG gerado no servidor: <${qr.tagName.toLowerCase()} src=${source}>`,
    ).toBe(true);
    expect(screen.getByText(testKey)).toBeTruthy();
    expect(screen.getByRole("button", { name: /copiar chave/i })).toBeTruthy();
    expect(screen.queryByText("O Pix do Subsolo chega em breve.")).toBeNull();
    expect(document.body.innerHTML).not.toContain("pix@subsolo.rocks");
  });

  it("SPEC-0028:UT-01 o QR code acompanha a chave da configuração", async () => {
    configurePix();
    stubApi(supporters);
    const first = await renderPage();
    const one = first.container.innerHTML;
    first.unmount();
    vi.resetModules();

    configurePix("outra-chave@subsolo.test");
    await renderPage();

    const qr = screen.getByRole("img", { name: qrName });
    expect(qr.outerHTML).not.toBe("");
    expect(one.includes(qr.outerHTML), "o QR code não mudou com a chave").toBe(
      false,
    );
  });

  it("SPEC-0028:UT-01 Copiar chave copia a chave e anuncia Chave copiada.", async () => {
    configurePix();
    stubApi(supporters);
    const writeText = vi
      .fn<(text: string) => Promise<void>>()
      .mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", {
      value: { writeText },
      configurable: true,
    });

    await renderPage();
    fireEvent.click(screen.getByRole("button", { name: /copiar chave/i }));

    const announced = await screen.findByText("Chave copiada.");
    expect(writeText).toHaveBeenCalledWith(testKey);
    expect(
      announced.closest(
        '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
      ),
      "Chave copiada. fora de uma região anunciada",
    ).not.toBeNull();
  });

  it("SPEC-0028:UT-01 sem a chave mostra O Pix do Subsolo chega em breve. no lugar do QR code", async () => {
    stubApi(supporters);

    await renderPage();

    expect(screen.getByText("O Pix do Subsolo chega em breve.")).toBeTruthy();
    expect(screen.queryByRole("img", { name: qrName })).toBeNull();
    expect(screen.queryByRole("button", { name: /copiar chave/i })).toBeNull();
    // A lista continua.
    expect(screen.getByText("Putridearth Zine")).toBeTruthy();
  });

  it("SPEC-0028:IT-01 a lista APOIADORES traz os nomes na ordem da API, com link só para quem tem", async () => {
    configurePix();
    stubApi(supporters);

    await renderPage();

    const heading = screen.getByRole("heading", { name: /^apoiadores$/i });
    const section =
      heading.closest("section") ?? heading.parentElement ?? document.body;
    const items = within(section as HTMLElement).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(items[0]?.textContent).toContain("Putridearth Zine");
    expect(items[1]?.textContent).toContain("Abismo Records");
    expect(
      within(section as HTMLElement)
        .getByRole("link", { name: /Putridearth Zine/ })
        .getAttribute("href"),
    ).toBe("https://zine.example");
    expect(
      within(section as HTMLElement).queryByRole("link", {
        name: /Abismo Records/,
      }),
    ).toBeNull();
  });

  it("SPEC-0028:IT-01 sem apoiadores mostra Seja o primeiro a apoiar.", async () => {
    configurePix();
    stubApi([]);

    await renderPage();

    expect(screen.getByRole("heading", { name: /^apoiadores$/i })).toBeTruthy();
    expect(screen.getByText("Seja o primeiro a apoiar.")).toBeTruthy();
  });

  it("SPEC-0028:IT-03 com a API fora do ar a página sai com o Pix e A lista de apoiadores volta já.", async () => {
    configurePix();
    stubApi("down");

    await renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: /^apoie o subsolo$/i }),
    ).toBeTruthy();
    expect(screen.getByRole("img", { name: qrName })).toBeTruthy();
    expect(screen.getByText(testKey)).toBeTruthy();
    expect(screen.getByText("A lista de apoiadores volta já.")).toBeTruthy();
    expect(screen.queryByText("Seja o primeiro a apoiar.")).toBeNull();
  });
});
