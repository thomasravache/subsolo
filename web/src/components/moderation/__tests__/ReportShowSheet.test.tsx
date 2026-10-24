// @vitest-environment jsdom
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeAll, describe, expect, it, vi } from "vitest";

import { reportShow } from "@/api/reports";
import { ReportShowSheet } from "@/components/moderation/ReportShowSheet";

vi.mock("@/api/reports", () => ({ reportShow: vi.fn() }));

/**
 * SPEC-0033 — a folha "Reportar show" (tela "Denunciar Show - Subsolo" do Stitch, §6), aberta pelo link "Reportar" da
 * página do show: um diálogo com rótulo, o texto "Ajude a manter a agenda do Subsolo correta.", os motivos como grupo de
 * rádio com rótulo ("Show falso", "Cancelado ou adiado e não avisaram", "Informação errada", "Outro"), "Conte mais
 * (opcional)", "Cancelar" e "Enviar denúncia".
 * - UT-01 (no navegador): "Outro" sem detalhe não envia e mostra o erro no campo; o detalhe aceita até 500 caracteres.
 * - IT-01 (resposta da API na tela): o envio manda o motivo e o detalhe (nulo quando vazio); 201 anuncia "Obrigado. A
 *   moderação vai olhar." e 409 `moderation.already_reported` anuncia "Você já denunciou este show." (em `aria-live`).
 * - `Esc` e "Cancelar" fecham (chamam `onClose`).
 * A folha é testada aberta (`open`); o link e a sessão ficam no E2E-01. O `showModal` do jsdom é completado aqui, para
 * a folha poder usar `<dialog>`.
 */
const showId = "0b7e4a9c-2d31-4f58-a6e0-5c9f1b3d7e21";

beforeAll(() => {
  const proto = HTMLDialogElement.prototype as HTMLDialogElement & {
    showModal?: () => void;
  };
  if (typeof proto.showModal !== "function") {
    proto.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute("open", "");
    };
  }
  if (typeof proto.close !== "function") {
    proto.close = function close(this: HTMLDialogElement) {
      this.removeAttribute("open");
      this.dispatchEvent(new Event("close"));
    };
  }
});

function renderSheet(onClose = vi.fn()) {
  render(
    <ReportShowSheet
      showId={showId}
      title="Noite da Peste II"
      open
      onClose={onClose}
    />,
  );
  return {
    onClose,
    dialog: screen.getByRole("dialog", { name: /reportar show/i }),
  };
}

function announced(text: RegExp): HTMLElement | undefined {
  return Array.from(
    document.querySelectorAll<HTMLElement>(
      '[aria-live="polite"], [aria-live="assertive"], [role="status"], [role="alert"]',
    ),
  ).find((element) => text.test(element.textContent ?? ""));
}

describe("folha Reportar show", () => {
  it("SPEC-0033:IT-01 é um diálogo com o texto, os quatro motivos num grupo de rádio e os botões", () => {
    const { dialog } = renderSheet();

    expect(dialog.textContent).toContain(
      "Ajude a manter a agenda do Subsolo correta.",
    );
    const group = screen.getByRole("radiogroup");
    expect(
      group.getAttribute("aria-label") ?? group.getAttribute("aria-labelledby"),
    ).toBeTruthy();
    for (const label of [
      "Show falso",
      "Cancelado ou adiado e não avisaram",
      "Informação errada",
      "Outro",
    ]) {
      expect(
        screen.getByRole("radio", { name: new RegExp(`^${label}`) }),
      ).toBeTruthy();
    }
    expect(
      screen.getByRole("textbox", { name: /conte mais \(opcional\)/i }),
    ).toBeTruthy();
    expect(screen.getByRole("button", { name: "Cancelar" })).toBeTruthy();
    expect(
      screen.getByRole("button", { name: "Enviar denúncia" }),
    ).toBeTruthy();
  });

  it("SPEC-0033:IT-01 envia o motivo escolhido (detalhe vazio vai nulo) e anuncia o agradecimento", async () => {
    vi.mocked(reportShow).mockResolvedValue({
      ok: true,
      value: "id-da-denuncia",
    });
    renderSheet();

    fireEvent.click(
      screen.getByRole("radio", {
        name: /^Cancelado ou adiado e não avisaram/,
      }),
    );
    fireEvent.click(screen.getByRole("button", { name: "Enviar denúncia" }));

    await waitFor(() =>
      expect(announced(/Obrigado\. A moderação vai olhar\./)).toBeDefined(),
    );
    expect(reportShow).toHaveBeenCalledWith(showId, {
      reason: "cancelled_unannounced",
      details: null,
    });
  });

  it("SPEC-0033:IT-01 a denúncia repetida (409) anuncia que o fã já denunciou", async () => {
    vi.mocked(reportShow).mockResolvedValue({
      ok: false,
      status: 409,
      code: "moderation.already_reported",
    });
    renderSheet();

    fireEvent.click(screen.getByRole("radio", { name: /^Show falso/ }));
    fireEvent.click(screen.getByRole("button", { name: "Enviar denúncia" }));

    await waitFor(() =>
      expect(announced(/Você já denunciou este show\./)).toBeDefined(),
    );
  });

  it("SPEC-0033:UT-01 Outro sem detalhe não envia e mostra o erro no campo", async () => {
    renderSheet();

    fireEvent.click(screen.getByRole("radio", { name: /^Outro/ }));
    fireEvent.click(screen.getByRole("button", { name: "Enviar denúncia" }));

    const details = screen.getByRole("textbox", { name: /conte mais/i });
    await waitFor(() =>
      expect(details.getAttribute("aria-invalid")).toBe("true"),
    );
    expect(reportShow).not.toHaveBeenCalled();
  });

  it("SPEC-0033:UT-01 o detalhe aceita até 500 caracteres e o envia", async () => {
    vi.mocked(reportShow).mockResolvedValue({ ok: true, value: "id" });
    renderSheet();
    const text = "a".repeat(500);

    fireEvent.click(screen.getByRole("radio", { name: /^Outro/ }));
    const details = screen.getByRole("textbox", { name: /conte mais/i });
    expect(details.getAttribute("maxlength")).toBe("500");
    fireEvent.change(details, { target: { value: text } });
    fireEvent.click(screen.getByRole("button", { name: "Enviar denúncia" }));

    await waitFor(() =>
      expect(reportShow).toHaveBeenCalledWith(showId, {
        reason: "other",
        details: text,
      }),
    );
  });

  it("SPEC-0033:IT-01 Esc e Cancelar fecham a folha", () => {
    const { onClose, dialog } = renderSheet();

    fireEvent.keyDown(dialog, { key: "Escape" });
    fireEvent(dialog, new Event("cancel"));
    expect(onClose).toHaveBeenCalled();

    onClose.mockClear();
    fireEvent.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onClose).toHaveBeenCalled();
  });
});
