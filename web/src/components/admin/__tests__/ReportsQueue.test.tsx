// @vitest-environment jsdom
import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import {
  hideReportedShow,
  keepReportedShow,
  type ReportQueueItem,
} from "@/api/reports";
import { ReportsQueue } from "@/components/admin/ReportsQueue";
import { reportMessages } from "@/components/moderation/messages";

vi.mock("@/api/reports", () => ({
  keepReportedShow: vi.fn(),
  hideReportedShow: vi.fn(),
}));

/**
 * SPEC-0033 — a aba "DENÚNCIAS (n)" da "Fila do admin" (tela "Fila do Admin - Subsolo" do Stitch, com os ajustes do §6):
 * - UT-02 (a fila agrupada na tela): um cartão (`article`) por show, com o título, "data • casa", "{n} denúncias" (ou
 *   "1 denúncia") com os motivos e os detalhes mais recentes, escapados (texto, nunca HTML), sem quem denunciou.
 * - IT-03 (as decisões na tela): "Manter" decide na hora; "Ocultar show" pede confirmação antes de decidir; o cartão
 *   decidido sai da fila e a decisão é anunciada.
 */
const keptId = "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21";
const hiddenId = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42";

const items: ReportQueueItem[] = [
  {
    showId: keptId,
    title: "Festival Ruína Total",
    citySlug: "curitiba",
    slug: "festival-ruina-total-2026-11-20",
    startsAtUtc: "2026-11-21T01:00:00Z",
    venueName: "Inferno Club",
    counts: { fake: 1, cancelled_unannounced: 0, wrong_info: 0, other: 1 },
    latestDetails: ["<b>Essa banda nem existe</b>"],
    firstReportedAtUtc: "2026-10-20T12:00:00Z",
  },
  {
    showId: hiddenId,
    title: "Necroculto Deathfest",
    citySlug: "curitiba",
    slug: "necroculto-deathfest-2026-11-25",
    startsAtUtc: "2026-11-26T01:00:00Z",
    venueName: "Espaço Underground",
    counts: { fake: 0, cancelled_unannounced: 1, wrong_info: 0, other: 0 },
    latestDetails: [],
    firstReportedAtUtc: "2026-10-21T12:00:00Z",
  },
];

function cardOf(title: string): HTMLElement {
  return screen.getByRole("article", { name: new RegExp(title) });
}

function announced(text: RegExp): HTMLElement | undefined {
  return Array.from(
    document.querySelectorAll<HTMLElement>(
      '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
    ),
  ).find((element) => text.test(element.textContent ?? ""));
}

describe("aba Denúncias da fila do admin", () => {
  it("SPEC-0033:UT-02 um cartão por show com data • casa, a contagem com os motivos e os detalhes como texto", () => {
    render(<ReportsQueue initialItems={items} />);

    expect(screen.getAllByRole("article")).toHaveLength(2);
    const kept = cardOf("Festival Ruína Total");
    expect(kept.textContent).toMatch(/Inferno Club/);
    expect(kept.textContent).toMatch(/•/);
    expect(kept.textContent).toMatch(/2 denúncias/);
    expect(kept.textContent).toMatch(/Show falso/);
    expect(kept.textContent).toMatch(/Outro/);
    expect(kept.textContent).toContain("<b>Essa banda nem existe</b>");
    expect(kept.querySelector("b")).toBeNull();
    const hidden = cardOf("Necroculto Deathfest");
    expect(hidden.textContent).toMatch(/1 denúncia(?!s)/);
    expect(hidden.textContent).toMatch(/Cancelado ou adiado e não avisaram/);
    expect(document.body.textContent ?? "").not.toMatch(/@|e-mail/i);
  });

  it("SPEC-0033:IT-03 Manter decide na hora, tira o cartão da fila e anuncia", async () => {
    vi.mocked(keepReportedShow).mockResolvedValue({
      ok: true,
      value: undefined,
    });
    render(<ReportsQueue initialItems={items} />);

    fireEvent.click(
      within(cardOf("Festival Ruína Total")).getByRole("button", {
        name: /manter/i,
      }),
    );

    await waitFor(() =>
      expect(
        screen.queryByRole("article", { name: /Festival Ruína Total/ }),
      ).toBeNull(),
    );
    expect(keepReportedShow).toHaveBeenCalledWith(keptId);
    expect(screen.getAllByRole("article")).toHaveLength(1);
    expect(announced(/./)).toBeDefined();
  });

  it("SPEC-0033:IT-03 Ocultar show pede confirmação antes de decidir e tira o cartão da fila", async () => {
    vi.mocked(hideReportedShow).mockResolvedValue({
      ok: true,
      value: undefined,
    });
    render(<ReportsQueue initialItems={items} />);
    const card = cardOf("Necroculto Deathfest");

    fireEvent.click(
      within(card).getByRole("button", { name: /ocultar show/i }),
    );
    expect(hideReportedShow).not.toHaveBeenCalled();

    const confirm = screen
      .getAllByRole("button", { name: /ocultar|confirmar/i })
      .find(
        (button) =>
          button !==
          within(card).queryByRole("button", { name: /^ocultar show$/i }),
      );
    expect(confirm, "sem o botão de confirmação").toBeDefined();
    fireEvent.click(confirm!);

    await waitFor(() =>
      expect(
        screen.queryByRole("article", { name: /Necroculto Deathfest/ }),
      ).toBeNull(),
    );
    expect(hideReportedShow).toHaveBeenCalledWith(
      hiddenId,
      expect.toSatisfy(
        (note: unknown) => note === null || typeof note === "string",
      ),
    );
  });
});

/**
 * SPEC-0037 UT-01 (H33-02) — depois de "Manter" ou "Ocultar show" o cartão decidido some; o foco vai para o título do
 * próximo cartão e, sem cartões, para a mensagem de fila vazia (nunca cai no `<body>`).
 */
describe("foco depois de decidir na aba Denúncias", () => {
  it("SPEC-0037:UT-01 o foco vai para o título do próximo cartão e, no último, para a fila vazia", async () => {
    vi.mocked(keepReportedShow).mockResolvedValue({
      ok: true,
      value: undefined,
    });
    vi.mocked(hideReportedShow).mockResolvedValue({
      ok: true,
      value: undefined,
    });
    render(<ReportsQueue initialItems={items} />);

    const keep = within(cardOf("Festival Ruína Total")).getByRole("button", {
      name: /manter/i,
    });
    keep.focus();
    fireEvent.click(keep);

    await waitFor(() =>
      expect(
        screen.queryByRole("article", { name: /Festival Ruína Total/ }),
      ).toBeNull(),
    );
    const next = within(cardOf("Necroculto Deathfest")).getByRole("heading", {
      name: /Necroculto Deathfest/,
    });
    await waitFor(() => {
      const active = document.activeElement;
      expect(
        active !== null && active !== document.body && next.contains(active),
        `o foco foi para ${active?.outerHTML.slice(0, 120)}`,
      ).toBe(true);
    });

    const last = cardOf("Necroculto Deathfest");
    const hide = within(last).getByRole("button", { name: /^ocultar show$/i });
    hide.focus();
    fireEvent.click(hide);
    const confirm = screen
      .getAllByRole("button", { name: /ocultar|confirmar/i })
      .find((button) => !last.contains(button));
    expect(confirm, "sem o botão de confirmação").toBeDefined();
    fireEvent.click(confirm!);

    await waitFor(() =>
      expect(screen.queryAllByRole("article")).toHaveLength(0),
    );
    await waitFor(() => {
      const active = document.activeElement;
      expect(active, "o foco caiu no body").not.toBe(document.body);
      expect(active?.textContent ?? "").toContain(reportMessages.empty);
    });
  });
});

/**
 * SPEC-0037 UT-03 (H33-06, Emenda 1) — o item da fila traz o fuso da cidade (`timeZone`) e o cartão mostra a data e a
 * hora do show nesse fuso: 23h30 UTC em Manaus (UTC−4) é 19h30, não as 20h30 de São Paulo (UTC−3).
 */
describe("data do show no fuso da cidade", () => {
  it("SPEC-0037:UT-03 o cartão de um show em America/Manaus às 23h30 UTC mostra 19h30", () => {
    const manaus = {
      ...items[0]!,
      title: "Noite do Norte",
      citySlug: "manaus",
      slug: "noite-do-norte-2026-11-20",
      startsAtUtc: "2026-11-20T23:30:00Z",
      timeZone: "America/Manaus",
    } as ReportQueueItem;

    render(<ReportsQueue initialItems={[manaus]} />);

    const text = cardOf("Noite do Norte").textContent ?? "";
    expect(text).toMatch(/20 de nov|20\/11/i);
    expect(text, "o cartão não mostra a hora no fuso de Manaus").toMatch(
      /19[:h]30/,
    );
    expect(text, "o cartão formatou no fuso de São Paulo").not.toMatch(
      /20[:h]30/,
    );
  });
});
