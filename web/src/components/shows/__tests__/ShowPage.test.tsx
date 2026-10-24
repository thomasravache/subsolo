// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import type { ShowDetail } from "@/api/show-detail";
import { ShowPage } from "@/components/shows/ShowPage";

/**
 * SPEC-0032 H29-03 (item herdado da revisão da SPEC-0029) — na parte "Ingresso" da página do show, a contagem do "vou"
 * ("{n} vão" / "{n} foram") fica num bloco próprio, separada do texto seguinte pela estrutura: nenhum filho direto da
 * seção é texto só com espaço, e a contagem é o texto inteiro do elemento dela. Show cancelado ("3 vão", sem o botão) e
 * show que já passou ("12 foram").
 *
 * SPEC-0036 UT-03 (H32-03) — o espaço que separa a contagem do texto seguinte fica dentro do parágrafo da contagem: em
 * nenhum bloco da seção "Ingresso" (a seção e todos os elementos dentro dela) há nó de texto só com espaço, e no
 * parágrafo da contagem "3 vão" vem seguido de espaço.
 */
function showDetail(status: ShowDetail["status"]): ShowDetail {
  return {
    id: "6d2f0c1a-9b84-4e3a-8f17-2c5e9a0b4d63",
    slug: "noite-da-peste-ii-2026-10-24",
    title: "Noite da Peste II",
    city: { slug: "curitiba", name: "Curitiba", timeZone: "America/Sao_Paulo" },
    startsAt: "2026-10-25T01:00:00Z",
    venue: { name: "Porão do Rock", profileSlug: null },
    lineup: [{ name: "Podridão", profileSlug: null }],
    styles: [{ slug: "death", name: "Death Metal" }],
    price: "R$ 30",
    ticketUrl: null,
    status,
    flyer: {
      thumbUrl: "/media/flyers/f-thumb.webp",
      fullUrl: "/media/flyers/f-full.webp",
      ogUrl: "/media/flyers/f-og.jpg",
      width: 1080,
      height: 1440,
    },
  } as ShowDetail;
}

function ticketSection(): HTMLElement {
  return screen.getByRole("region", { name: "Ingresso" });
}

function looseWhitespace(section: HTMLElement): Node[] {
  return Array.from(section.childNodes).filter(
    (node) =>
      node.nodeType === Node.TEXT_NODE &&
      (node.textContent ?? "").trim() === "",
  );
}

describe("SPEC-0032:H29-03 contagem do vou num bloco próprio", () => {
  it("SPEC-0032:H29-03 show cancelado: a contagem é um elemento próprio, sem nó de texto solto depois dela", () => {
    render(
      <ShowPage
        show={showDetail("cancelled")}
        url="https://localhost/curitiba/noite-da-peste-ii-2026-10-24"
        attendingCount={3}
      />,
    );

    const section = ticketSection();
    expect(looseWhitespace(section)).toHaveLength(0);
    const count = screen.getByText("3 vão");
    expect(count.textContent?.trim()).toBe("3 vão");
    expect(section.contains(count)).toBe(true);
  });

  it('SPEC-0032:H29-03 show que já passou: "{n} foram" num elemento próprio, sem nó de texto solto', () => {
    render(
      <ShowPage
        show={showDetail("scheduled")}
        url="https://localhost/curitiba/noite-da-peste-ii-2026-10-24"
        attendingCount={12}
        past
      />,
    );

    const section = ticketSection();
    expect(looseWhitespace(section)).toHaveLength(0);
    expect(screen.getByText("12 foram").textContent?.trim()).toBe("12 foram");
  });
});

/** Os nós de texto só com espaço de `root` e de todos os elementos dentro dele. */
function whitespaceOnlyTextNodes(root: HTMLElement): Node[] {
  return [root, ...Array.from(root.querySelectorAll("*"))].flatMap((element) =>
    looseWhitespace(element as HTMLElement),
  );
}

describe("SPEC-0036:UT-03 espaço da contagem dentro do parágrafo", () => {
  it('SPEC-0036:UT-03 show com 3 "vou": nenhum bloco da seção "Ingresso" tem nó de texto só com espaço e "3 vão" vem seguido de espaço no mesmo parágrafo', () => {
    render(
      <ShowPage
        show={showDetail("cancelled")}
        url="https://localhost/curitiba/noite-da-peste-ii-2026-10-24"
        attendingCount={3}
      />,
    );

    const section = ticketSection();
    expect(
      whitespaceOnlyTextNodes(section).map((node) =>
        (node.parentElement?.outerHTML ?? "").slice(0, 120),
      ),
    ).toEqual([]);
    const paragraph = screen.getByText("3 vão").closest("p");
    expect(paragraph, "a contagem não está num parágrafo").not.toBeNull();
    expect(section.contains(paragraph)).toBe(true);
    expect(paragraph?.textContent ?? "").toMatch(/3 vão\s/);
  });
});
