// @vitest-environment jsdom
import {
  act,
  fireEvent,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Itens herdados da revisão da SPEC-0027 (G4) no botão de seguir e na lista "Seguindo", com a API respondendo por um
 * `fetch` falso (o cliente do navegador usa caminhos relativos; o `Request` resolve contra a página):
 * - H27-01 — o efeito que lê `GET /api/v1/me/follows` depende de valores primitivos do alvo (`kind` e `slug ?? id`):
 *   re-renderizar com um objeto `target` novo, mas igual, não refaz a consulta.
 * - H27-02 — durante a requisição de seguir (e do "Deixar de seguir" da lista), o botão fica com `aria-disabled="true"`
 *   e ignora cliques, em vez de `disabled` (que tira o foco do teclado); terminada, volta a aceitar.
 * - H27-03 — a contagem acompanha a prop `followers` quando ela muda.
 * - H27-04 — nome acessível constante "Seguir {nome}" com `aria-pressed` (WCAG 2.5.3); o texto visível vira "Seguindo"
 *   quando seguido.
 */

type Call = { method: string; path: string };

const calls: Call[] = [];

/** Respostas pendentes das mutações (para observar o botão durante a requisição). */
let releaseMutation: (() => void) | undefined;

function pathOf(input: Parameters<typeof fetch>[0]): string {
  return new URL(
    typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url,
    window.location.href,
  ).pathname;
}

function methodOf(
  input: Parameters<typeof fetch>[0],
  init?: Parameters<typeof fetch>[1],
): string {
  return (
    init?.method ??
    (typeof input === "object" && "method" in input ? input.method : "GET")
  ).toUpperCase();
}

/** A API: `/me/follows` com `follows`; antiforgery; mutações 204 (seguras até `releaseMutation`, se `hold`). */
function stubApi(
  follows: { cities: { slug: string; name: string }[]; profiles: [] },
  hold = false,
) {
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const call = { method: methodOf(input, init), path: pathOf(input) };
    calls.push(call);
    if (call.path === "/api/v1/antiforgery") {
      return Response.json({ token: "csrf-token" });
    }
    if (call.path === "/api/v1/me/follows") {
      return Response.json(follows);
    }
    if (call.method === "PUT" || call.method === "DELETE") {
      if (hold) {
        await new Promise<void>((resolve) => {
          releaseMutation = resolve;
        });
      }
      return new Response(null, { status: 204 });
    }
    throw new TypeError("fetch failed");
  });
}

const curitiba = { slug: "curitiba", name: "Curitiba" };

const mutations = () =>
  calls.filter((call) => call.method === "PUT" || call.method === "DELETE");

beforeEach(() => {
  calls.length = 0;
  releaseMutation = undefined;
  const Native = globalThis.Request;
  vi.stubGlobal(
    "Request",
    class extends Native {
      constructor(input: RequestInfo | URL, init?: RequestInit) {
        super(
          typeof input === "string"
            ? new URL(input, window.location.href)
            : input,
          init,
        );
      }
    },
  );
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function loadFollowButton() {
  return (await import("@/components/engagement/FollowButton")).FollowButton;
}

describe("botão de seguir", () => {
  it("SPEC-0029:H27-01 re-renderizar com um alvo igual (objeto novo) não refaz GET /me/follows", async () => {
    stubApi({ cities: [], profiles: [] });
    const FollowButton = await loadFollowButton();
    const view = render(
      <FollowButton
        target={{ kind: "city", slug: "curitiba" }}
        name="Curitiba"
        returnTo="/curitiba"
      />,
    );
    await waitFor(() =>
      expect(
        calls.filter((call) => call.path === "/api/v1/me/follows"),
      ).toHaveLength(1),
    );

    for (let i = 0; i < 3; i++) {
      view.rerender(
        <FollowButton
          target={{ kind: "city", slug: "curitiba" }}
          name="Curitiba"
          returnTo="/curitiba"
        />,
      );
    }
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20));
    });

    expect(
      calls.filter((call) => call.path === "/api/v1/me/follows"),
      "GET /me/follows refeito a cada re-renderização",
    ).toHaveLength(1);
  });

  it("SPEC-0029:H27-02 durante a requisição o botão fica aria-disabled (sem disabled) e ignora cliques", async () => {
    stubApi({ cities: [], profiles: [] }, true);
    const FollowButton = await loadFollowButton();
    render(
      <FollowButton
        target={{ kind: "city", slug: "curitiba" }}
        name="Curitiba"
        label="Seguir Curitiba"
        returnTo="/curitiba"
      />,
    );
    const button = screen.getByRole("button", { name: "Seguir Curitiba" });
    // A sessão carregou (o botão aceita o toque).
    await waitFor(() =>
      expect(
        button.hasAttribute("disabled") ||
          button.getAttribute("aria-disabled") === "true",
      ).toBe(false),
    );

    button.focus();
    await act(async () => {
      fireEvent.click(button);
    });
    await waitFor(() => expect(mutations()).toHaveLength(1));

    expect(button.getAttribute("aria-disabled")).toBe("true");
    expect(button.hasAttribute("disabled"), "disabled tira o foco").toBe(false);
    expect(document.activeElement).toBe(button);
    await act(async () => {
      fireEvent.click(button);
    });
    expect(mutations(), "clique durante a requisição foi enviado").toHaveLength(
      1,
    );

    await act(async () => {
      releaseMutation?.();
    });
    await waitFor(() =>
      expect(button.getAttribute("aria-disabled")).not.toBe("true"),
    );
    expect(button.getAttribute("aria-pressed")).toBe("true");
  });

  it("SPEC-0029:H27-03 a contagem acompanha a prop followers", async () => {
    stubApi({ cities: [], profiles: [] });
    const FollowButton = await loadFollowButton();
    const target = { kind: "profile" as const, id: "0199f0c1-6a4d-7e7f" };
    const view = render(
      <FollowButton
        target={target}
        name="Podridão"
        followers={3}
        returnTo="/banda/podridao"
      />,
    );
    expect(screen.getByText("3 seguidores")).toBeTruthy();

    view.rerender(
      <FollowButton
        target={target}
        name="Podridão"
        followers={5}
        returnTo="/banda/podridao"
      />,
    );

    await waitFor(() => expect(screen.getByText("5 seguidores")).toBeTruthy());
    expect(screen.queryByText("3 seguidores")).toBeNull();
  });

  it('SPEC-0029:H27-04 o nome acessível é sempre "Seguir {nome}"; seguido, aria-pressed e o texto "Seguindo"', async () => {
    stubApi({ cities: [curitiba], profiles: [] });
    const FollowButton = await loadFollowButton();
    render(
      <FollowButton
        target={{ kind: "city", slug: "curitiba" }}
        name="Curitiba"
        label="Seguir Curitiba"
        returnTo="/curitiba"
      />,
    );

    const button = await screen.findByRole("button", {
      name: "Seguir Curitiba",
      pressed: true,
    });
    expect(button.textContent?.trim()).toMatch(/^seguindo$/i);
    expect(
      screen.queryByRole("button", { name: /deixar de seguir/i }),
    ).toBeNull();

    await act(async () => {
      fireEvent.click(button);
    });

    const after = await screen.findByRole("button", {
      name: "Seguir Curitiba",
      pressed: false,
    });
    expect(after.textContent?.trim()).toMatch(/^seguir curitiba$/i);
  });
});

describe("lista Seguindo", () => {
  it('SPEC-0029:H27-02 durante a requisição o "Deixar de seguir" fica aria-disabled (sem disabled) e ignora cliques', async () => {
    stubApi({ cities: [curitiba], profiles: [] }, true);
    const { FollowingList } =
      await import("@/components/engagement/FollowingList");
    render(<FollowingList initial={{ cities: [curitiba], profiles: [] }} />);
    const button = screen.getByRole("button", { name: /curitiba/i });

    button.focus();
    await act(async () => {
      fireEvent.click(button);
    });
    await waitFor(() => expect(mutations()).toHaveLength(1));

    expect(button.getAttribute("aria-disabled")).toBe("true");
    expect(button.hasAttribute("disabled"), "disabled tira o foco").toBe(false);
    await act(async () => {
      fireEvent.click(button);
    });
    expect(mutations(), "clique durante a requisição foi enviado").toHaveLength(
      1,
    );

    await act(async () => {
      releaseMutation?.();
    });
    await waitFor(() =>
      expect(screen.queryByRole("button", { name: /curitiba/i })).toBeNull(),
    );
  });
});
