// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { expectIndexWithAnchors } from "@/components/site/__tests__/institutional-index.test.helper";

/**
 * SPEC-0028 E2E-02 (parte da página) — `/privacidade` (SSR estática) traz o h1 "PRIVACIDADE", "Atualizada em {data}",
 * o índice com âncoras para "Que dados guardamos", "Para quê", "Por quanto tempo", "Seus direitos" e "Contato", o
 * conteúdo do ADR-0008 que o §6 cita (sem anúncios nem rastreamento; backups somem em até 6 meses; exportar e excluir
 * os dados na conta) e o canal do titular: o e-mail de `Site:ContactEmail` (no ambiente do servidor,
 * `Site__ContactEmail`).
 */
const contactEmail = "contato-teste@subsolo.test";

const sections = [
  "Que dados guardamos",
  "Para quê",
  "Por quanto tempo",
  "Seus direitos",
  "Contato",
];

async function renderPage() {
  const { default: PrivacidadePage } = await import("./page");
  return render(await PrivacidadePage());
}

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("página Privacidade", () => {
  it("SPEC-0028:E2E-02 h1, data de atualização e índice com âncoras das seções", async () => {
    vi.stubEnv("Site__ContactEmail", contactEmail);

    await renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: /^privacidade$/i }),
    ).toBeTruthy();
    expect(screen.getByText(/^Atualizada em \S.+/)).toBeTruthy();
    expectIndexWithAnchors(sections);
  });

  it("SPEC-0028:E2E-02 o conteúdo cita o ADR-0008: sem anúncios nem rastreamento, backups em até 6 meses, exportar e excluir", async () => {
    vi.stubEnv("Site__ContactEmail", contactEmail);

    await renderPage();

    const text = document.body.textContent ?? "";
    expect(text).toMatch(/anúncios/i);
    expect(text).toMatch(/rastreamento/i);
    expect(text).toMatch(/6 meses/);
    expect(text).toMatch(/export/i);
    expect(text).toMatch(/exclu/i);
  });

  it("SPEC-0028:E2E-02 o canal do titular é o e-mail da configuração", async () => {
    vi.stubEnv("Site__ContactEmail", contactEmail);

    await renderPage();

    const mail = screen.getByRole("link", { name: new RegExp(contactEmail) });
    expect(mail.getAttribute("href")).toBe(`mailto:${contactEmail}`);
  });
});
