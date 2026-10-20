// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { expectIndexWithAnchors } from "@/components/site/__tests__/institutional-index.test.helper";

/**
 * SPEC-0028 E2E-02 (parte da página) — `/termos` (SSR estática, no modelo da tela "Privacidade - Subsolo") traz o h1
 * "TERMOS DE USO", "Atualizada em {data}", o índice com âncoras para "O que é o Subsolo", "Conta", "Quem cadastra
 * shows", "Conteúdo proibido", "Moderação" e "Mudanças", e o conteúdo proibido do §6 (discurso de ódio, NSBM, show
 * falso).
 */
const sections = [
  "O que é o Subsolo",
  "Conta",
  "Quem cadastra shows",
  "Conteúdo proibido",
  "Moderação",
  "Mudanças",
];

async function renderPage() {
  const { default: TermosPage } = await import("./page");
  return render(await TermosPage());
}

afterEach(() => {
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("página Termos de uso", () => {
  it("SPEC-0028:E2E-02 h1, data de atualização e índice com âncoras das seções", async () => {
    vi.stubEnv("Site__ContactEmail", "contato-teste@subsolo.test");

    await renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: /^termos de uso$/i }),
    ).toBeTruthy();
    expect(screen.getByText(/^Atualizada em \S.+/)).toBeTruthy();
    expectIndexWithAnchors(sections);
  });

  it("SPEC-0028:E2E-02 o conteúdo proibido cita discurso de ódio, NSBM e show falso", async () => {
    await renderPage();

    const text = document.body.textContent ?? "";
    expect(text).toMatch(/discurso de ódio/i);
    expect(text).toMatch(/NSBM/);
    expect(text).toMatch(/show falso/i);
  });
});
