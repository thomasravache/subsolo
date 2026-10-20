import { screen, within } from "@testing-library/react";
import { expect } from "vitest";

/** Escapa um texto para usar numa expressão regular. */
function literal(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/**
 * SPEC-0028 — as páginas institucionais (`/privacidade`, `/termos`) têm um índice com âncoras: para cada seção, um link
 * do índice (`#id`) com o nome dela leva a um elemento com esse `id` que contém o título (heading) da seção.
 */
export function expectIndexWithAnchors(sections: string[]): void {
  for (const name of sections) {
    const pattern = new RegExp(`^${literal(name)}$`, "i");
    const heading = screen.getByRole("heading", { name: pattern });
    const links = screen
      .getAllByRole("link", { name: pattern })
      .filter((link) => (link.getAttribute("href") ?? "").startsWith("#"));
    expect(links, `índice sem link para "${name}"`).toHaveLength(1);
    const id = (links[0]?.getAttribute("href") ?? "").slice(1);
    expect(id, `âncora vazia para "${name}"`).not.toBe("");
    const target = document.getElementById(id);
    expect(target, `#${id} não existe na página`).not.toBeNull();
    expect(
      target === heading || target?.contains(heading),
      `#${id} não é a seção "${name}"`,
    ).toBe(true);
  }
  const index = screen
    .getAllByRole("link")
    .filter((link) => (link.getAttribute("href") ?? "").startsWith("#"))[0]
    ?.closest("nav");
  expect(index, "o índice não está num <nav>").toBeTruthy();
  expect(
    within(index as HTMLElement).getAllByRole("link").length,
  ).toBeGreaterThanOrEqual(sections.length);
}
