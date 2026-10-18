// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { createCity } from "@/api/admin";

import { CitiesPanel } from "../CitiesPanel";

vi.mock("@/api/admin", () => ({
  createCity: vi.fn(
    async (input: { name: string; state: string; timeZone: string }) => ({
      ok: true,
      value: {
        slug: "sao-jose-dos-pinhais",
        name: input.name,
        state: input.state,
        timeZone: input.timeZone,
      },
    }),
  ),
  updateCity: vi.fn(),
}));

/**
 * SPEC-0017 UT-01 (Emenda 2) — no formulário "Nova cidade", a prévia do endereço usa o domínio decidido
 * (`agendasubsolo.com.br/{slug}`, ADR-0004) e segue o nome; um endereço editado e depois apagado volta ao gerado do
 * nome (no campo e na prévia), e a criação manda `slug: null`.
 */
function openNewCity() {
  render(<CitiesPanel initialCities={[]} onSuccess={() => {}} />);
  fireEvent.click(screen.getByRole("button", { name: "Nova cidade" }));
  const form = screen.getByRole("form", { name: "Nova cidade" });
  fireEvent.change(screen.getByLabelText("Nome"), {
    target: { value: "São José dos Pinhais" },
  });
  return form;
}

/** A prévia termina em `/{slug}` (o domínio é conferido no teste da prévia). */
function preview(slug: string): HTMLElement {
  return screen.getByText(new RegExp(`/${slug}$`));
}

function address(): HTMLInputElement {
  return screen.getByLabelText("Endereço");
}

describe("prévia do endereço da cidade", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("SPEC-0017:UT-01 a prévia mostra agendasubsolo.com.br/{slug} gerado do nome", () => {
    openNewCity();

    expect(address().value).toBe("sao-jose-dos-pinhais");
    expect(
      screen.getByText("agendasubsolo.com.br/sao-jose-dos-pinhais"),
    ).toBeTruthy();
  });

  it("SPEC-0017:UT-01 apagar o endereço editado volta ao gerado do nome", () => {
    openNewCity();

    fireEvent.change(address(), { target: { value: "sjp" } });
    expect(preview("sjp")).toBeTruthy();

    fireEvent.change(address(), { target: { value: "" } });

    expect(address().value).toBe("sao-jose-dos-pinhais");
    expect(preview("sao-jose-dos-pinhais")).toBeTruthy();

    // Depois de apagado, o endereço volta a seguir o nome.
    fireEvent.change(screen.getByLabelText("Nome"), {
      target: { value: "Foz do Iguaçu" },
    });
    expect(address().value).toBe("foz-do-iguacu");
    expect(preview("foz-do-iguacu")).toBeTruthy();
  });

  it("SPEC-0017:UT-01 com o endereço apagado a criação manda slug null", async () => {
    openNewCity();
    fireEvent.change(address(), { target: { value: "sjp" } });
    fireEvent.change(address(), { target: { value: "" } });
    fireEvent.change(screen.getByLabelText("UF"), { target: { value: "PR" } });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Criar cidade" }));
    });

    expect(createCity).toHaveBeenCalledWith({
      name: "São José dos Pinhais",
      state: "PR",
      timeZone: "America/Sao_Paulo",
      slug: null,
    });
  });
});
