// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import type { PublicProfile } from "@/api/public-profile";
import { ProfilePage } from "@/components/profiles/ProfilePage";

/**
 * SPEC-0026 IT-05 (parte da página) — com o endpoint de shows fora do ar (as listas chegam como `null`), a página do
 * perfil aparece mesmo assim: o nome, a cidade e os links das redes, e o aviso "Não foi possível carregar os shows
 * agora." no lugar das listas (sem "Nenhum show anunciado ainda.", que diria que o perfil não tem shows).
 */
const band: PublicProfile = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
  type: "band",
  name: "Podridão",
  slug: "podridao",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [{ slug: "death", name: "Death Metal" }],
  links: { instagram: "https://www.instagram.com/podridao" },
};

describe("página do perfil sem a API de shows", () => {
  it("SPEC-0026:IT-05 mostra o perfil e o aviso no lugar das listas", () => {
    render(<ProfilePage profile={band} upcoming={null} past={null} />);

    expect(
      screen.getByRole("heading", { level: 1, name: /podridão/i }),
    ).toBeTruthy();
    expect(screen.getAllByText(/curitiba/i).length).toBeGreaterThan(0);
    const instagram = screen.getByRole("link", {
      name: "Instagram de Podridão",
    });
    expect(instagram.getAttribute("href")).toBe(
      "https://www.instagram.com/podridao",
    );
    expect(instagram.getAttribute("rel") ?? "").toContain("noopener");
    expect(instagram.getAttribute("rel") ?? "").toContain("noreferrer");
    expect(
      screen.getAllByText("Não foi possível carregar os shows agora.").length,
    ).toBeGreaterThan(0);
    expect(screen.queryByText("Nenhum show anunciado ainda.")).toBeNull();
  });
});
