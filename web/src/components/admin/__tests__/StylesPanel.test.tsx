// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { renameStyle } from "@/api/admin";

import { StylesPanel } from "../StylesPanel";

vi.mock("@/api/admin", () => ({
  createStyle: vi.fn(),
  renameStyle: vi.fn(async (slug: string, name: string) => ({
    ok: true,
    value: { slug, name },
  })),
}));

/**
 * SPEC-0017 UT-03 (Emenda 2) — depois de "Salvar" ou "Cancelar" no formulário de renomear do chip, o foco volta ao botão
 * "Renomear {nome}" daquele estilo (com o nome novo, depois de salvar), e não se perde no `body`.
 */
const styles = [
  { slug: "crust", name: "Crust" },
  { slug: "doom", name: "doom" },
];

function openRename() {
  render(<StylesPanel initialStyles={styles} onSuccess={() => {}} />);
  fireEvent.click(screen.getByRole("button", { name: "Renomear Crust" }));
  expect(screen.getByRole("button", { name: "Salvar" })).toBeTruthy();
}

describe("foco depois de renomear um estilo", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("SPEC-0017:UT-03 Cancelar devolve o foco ao botão Renomear do estilo", () => {
    openRename();

    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));

    expect(document.activeElement).toBe(
      screen.getByRole("button", { name: "Renomear Crust" }),
    );
  });

  it("SPEC-0017:UT-03 Salvar devolve o foco ao botão Renomear com o nome novo", async () => {
    openRename();
    fireEvent.change(screen.getByRole("textbox", { name: "Renomear Crust" }), {
      target: { value: "Crust Punk" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Salvar" }));
    });

    expect(renameStyle).toHaveBeenCalledWith("crust", "Crust Punk");
    expect(document.activeElement).toBe(
      screen.getByRole("button", { name: "Renomear Crust Punk" }),
    );
  });
});
