// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { requestProfile } from "@/api/profiles";

import { ProfileForm } from "../ProfileForm";

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

vi.mock("@/api/profiles", () => ({
  requestProfile: vi.fn(async () => ({
    ok: false,
    code: "profiles.invalid_request",
    status: 400,
    fields: { Styles: ["Escolha de 1 a 5 estilos."] },
  })),
  createOwnerlessProfile: vi.fn(),
}));

/**
 * SPEC-0021 E2E-01 (Emenda 3, §7.6: o grupo de estilos recebe o id do campo para o foco no erro) — no formulário
 * "Pedir perfil", quando a API recusa só os estilos (`profiles.invalid_request` no campo `Styles`), a mensagem aparece
 * ligada ao grupo "Estilos" e o foco vai para o grupo (não fica no botão nem se perde no `body`).
 */
const cities = [{ slug: "cidade-exemplo", name: "Cidade Exemplo" }];
const styles = [
  { slug: "death", name: "death" },
  { slug: "grind", name: "grind" },
];

describe("foco no erro dos estilos", () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it("SPEC-0021:E2E-01 erro nos estilos leva o foco ao grupo Estilos", async () => {
    render(
      <ProfileForm mode="request" cities={cities as never} styles={styles} />,
    );
    fireEvent.click(screen.getByRole("radio", { name: "Banda" }));
    fireEvent.change(screen.getByLabelText("Nome"), {
      target: { value: "Podridão" },
    });

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Enviar pedido" }));
    });

    expect(requestProfile).toHaveBeenCalledTimes(1);
    const group = screen.getByRole("group", { name: "Estilos" });
    expect(screen.getByText("Escolha de 1 a 5 estilos.")).toBeTruthy();
    expect(group.getAttribute("aria-describedby") ?? "").toContain(
      screen.getByText("Escolha de 1 a 5 estilos.").id,
    );
    expect(document.activeElement).toBe(group);
  });
});
