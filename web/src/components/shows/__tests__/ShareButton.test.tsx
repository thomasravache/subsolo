// @vitest-environment jsdom
import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

import { ShareButton } from "@/components/shows/ShareButton";

/**
 * SPEC-0024 UT-02 — o botão "Compartilhar" da página do show: com a Web Share API (`navigator.share`, no celular)
 * chama o compartilhamento com o título e a URL do show; sem ela, copia o link (`navigator.clipboard.writeText`) e
 * anuncia "Link copiado." numa região `aria-live`.
 */
const title = "Noite da Peste II";
const url = "https://subsolo.example/curitiba/noite-da-peste-ii-2026-11-14";

function setNavigator(name: "share" | "clipboard", value: unknown) {
  Object.defineProperty(window.navigator, name, {
    value,
    configurable: true,
    writable: true,
  });
}

describe("Compartilhar", () => {
  afterEach(() => {
    setNavigator("share", undefined);
    setNavigator("clipboard", undefined);
  });

  it("SPEC-0024:UT-02 com Web Share, compartilha o título e a URL do show", async () => {
    const share = vi.fn(async () => undefined);
    const writeText = vi.fn(async () => undefined);
    setNavigator("share", share);
    setNavigator("clipboard", { writeText });
    render(<ShareButton title={title} url={url} />);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /compartilhar/i }));
    });

    expect(share).toHaveBeenCalledTimes(1);
    expect(share).toHaveBeenCalledWith(
      expect.objectContaining({ url, title: expect.stringContaining(title) }),
    );
    expect(writeText).not.toHaveBeenCalled();
  });

  it('SPEC-0024:UT-02 sem Web Share, copia o link e anuncia "Link copiado." em aria-live', async () => {
    const writeText = vi.fn(async () => undefined);
    setNavigator("share", undefined);
    setNavigator("clipboard", { writeText });
    render(<ShareButton title={title} url={url} />);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: /compartilhar/i }));
    });

    expect(writeText).toHaveBeenCalledWith(url);
    const message = await screen.findByText("Link copiado.");
    expect(
      message.closest("[aria-live]"),
      '"Link copiado." fora de uma região aria-live',
    ).not.toBeNull();
  });
});
