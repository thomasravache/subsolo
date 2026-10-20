// @vitest-environment jsdom
import { render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

/**
 * SPEC-0029 H27-05 (revisão da SPEC-0027) — "Seguindo" também no desktop e a aba ativa decidida num lugar só:
 * - o cabeçalho (`Header`, navegação do desktop) tem o link "Seguindo" → `/seguindo`, com ou sem sessão;
 * - em `/seguindo`, o `AppShell` calcula o destino ativo (`currentDestination`) e a navegação inferior marca "Seguindo"
 *   com `aria-current="page"`; o `BottomNav` não deduz mais o destino pela URL (sem `current`, nada fica ativo).
 */
const pathname = { value: "/seguindo" };

vi.mock("next/navigation", () => ({
  usePathname: () => pathname.value,
  useParams: () => ({}),
}));

vi.mock("@/api/account", () => ({
  hasSession: async () => true,
}));

afterEach(() => {
  pathname.value = "/seguindo";
});

const headerNav = () => screen.getByRole("navigation", { name: "Navegação" });

const bottomNav = () =>
  screen.getByRole("navigation", { name: "Navegação inferior" });

describe("Seguindo na navegação", () => {
  it.each([true, false])(
    "SPEC-0029:H27-05 o cabeçalho do desktop tem o link Seguindo → /seguindo (com sessão: %s)",
    async (signedIn) => {
      const { Header } = await import("@/components/ui/Header");
      render(
        <Header
          city="curitiba"
          cities={[]}
          onCityChange={() => undefined}
          signedIn={signedIn}
        />,
      );

      const link = within(headerNav()).getByRole("link", {
        name: /seguindo/i,
      });
      expect(link.getAttribute("href")).toBe("/seguindo");
    },
  );

  it("SPEC-0029:H27-05 em /seguindo, o AppShell marca a aba Seguindo como ativa", async () => {
    pathname.value = "/seguindo";
    const { AppShell } = await import("@/components/ui/AppShell");
    render(
      <AppShell cities={[]}>
        <main id="conteudo" />
      </AppShell>,
    );

    const nav = bottomNav();
    expect(
      within(nav)
        .getByRole("link", { name: /seguindo/i })
        .getAttribute("aria-current"),
    ).toBe("page");
    expect(
      within(nav)
        .getAllByRole("link")
        .filter((link) => link.getAttribute("aria-current") === "page"),
    ).toHaveLength(1);
  });

  it("SPEC-0029:H27-05 o BottomNav não deduz o destino pela URL: sem current, nada fica ativo", async () => {
    pathname.value = "/seguindo";
    const { BottomNav } = await import("@/components/ui/BottomNav");
    render(<BottomNav city="curitiba" signedIn />);

    expect(
      within(bottomNav())
        .getAllByRole("link")
        .filter((link) => link.getAttribute("aria-current") === "page"),
      "o BottomNav calculou o destino ativo sozinho",
    ).toHaveLength(0);
  });
});
