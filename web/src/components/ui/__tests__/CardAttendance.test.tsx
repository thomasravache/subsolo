// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Card, type CardProps } from "@/components/ui/Card";

/**
 * SPEC-0029 IT-05 (Emenda 1) — o `Card` aceita a contagem ausente (contagem do `Engagement` indisponível): sem
 * `attendingCount`, nenhum texto "{n} vão"/"vai"; com ela, "{n} vão" (e "1 vai").
 */
const base = {
  href: "/curitiba/noite-da-peste-ii",
  flyerSrc: null,
  title: "Noite da Peste II",
  date: "2026-11-15T00:00:00Z",
  timeZone: "America/Sao_Paulo",
  venue: "Porão do Rock",
  lineup: ["Cova Rasa"],
  styles: ["Death"],
  status: "scheduled" as const,
};

const article = () => screen.getByRole("article");

describe("Card com a contagem de vão", () => {
  it("SPEC-0029:IT-05 sem contagem, o card não mostra texto de vão", () => {
    render(<Card {...(base as unknown as CardProps)} />);

    expect(article().textContent ?? "").not.toMatch(/(vão|vai)/i);
    expect(article().textContent ?? "").not.toMatch(/undefined|NaN/);
  });

  it.each([
    [47, /47\s*vão/i],
    [1, /1\s*vai/i],
    [0, /0\s*vão/i],
  ])(
    "SPEC-0029:IT-05 com a contagem %i, o card mostra o número",
    (count, text) => {
      render(<Card {...base} attendingCount={count} />);

      expect(article().textContent ?? "").toMatch(text);
    },
  );
});
