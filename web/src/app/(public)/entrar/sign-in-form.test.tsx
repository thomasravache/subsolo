// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { SignInForm } from "./sign-in-form";

vi.mock("@/api/account", () => ({
  requestMagicLink: vi.fn(async () => ({ ok: true })),
}));

/**
 * SPEC-0016 (Emenda 4, §7.6 l; acessibilidade do §5) — no estado "Confira seu e-mail", o contador de reenvio é anunciado
 * pela região `aria-live="polite"` só no começo e no fim: o texto visível pode contar os segundos, mas a região viva
 * não muda a cada segundo (um leitor de tela leria 60 anúncios). No fim, a região anuncia que já dá para reenviar.
 */

/** Texto de todas as regiões vivas da tela, na ordem do documento. */
function liveText(): string {
  return [...document.querySelectorAll("[aria-live]")]
    .map((region) => region.textContent ?? "")
    .join(" | ");
}

async function sendLink() {
  render(<SignInForm returnTo={null} />);
  fireEvent.change(screen.getByLabelText("Seu e-mail"), {
    target: { value: "fa@subsolo.test" },
  });
  await act(async () => {
    fireEvent.click(
      screen.getByRole("button", { name: "Mandar link de acesso" }),
    );
  });
  expect(
    screen.getByRole("heading", { name: /confira seu e-mail/i }),
  ).toBeTruthy();
}

describe("contador de reenvio", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: false });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("SPEC-0016 (§7.6 l) a região viva não é atualizada a cada segundo", async () => {
    await sendLink();
    const atStart = liveText();
    expect(
      atStart,
      "sem região aria-live no estado Confira seu e-mail",
    ).not.toBe("");

    const seen = new Set<string>();
    for (let second = 1; second <= 50; second++) {
      await act(async () => {
        vi.advanceTimersByTime(1_000);
      });
      seen.add(liveText());
    }

    expect(
      [...seen],
      "a região viva mudou durante a contagem (anúncio a cada segundo)",
    ).toEqual([atStart]);
  });

  it("SPEC-0016 (§7.6 l) a região viva anuncia o fim da espera", async () => {
    await sendLink();
    const atStart = liveText();

    await act(async () => {
      vi.advanceTimersByTime(61_000);
    });

    expect(liveText()).not.toBe(atStart);
    expect(
      screen.getByRole("button", { name: /reenviar link/i }),
    ).not.toHaveProperty("disabled", true);
  });
});
