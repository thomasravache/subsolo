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
 * SPEC-0029 — "Vou" e a contagem na página do show (`/{cidade}/{show}`), com a API respondendo por um `fetch` falso
 * (servidor em `API_URL`; navegador em caminhos relativos, com o `Request` resolvendo contra a página):
 * - E2E-01 (parte do botão) — show agendado e futuro: botão de nome constante "Vou ao show {título}" (H27-04) com
 *   `aria-pressed`, texto visível "Vou" e a contagem "{n} vão" (de `GET /api/v1/attendances/counts`); o fã logado toca:
 *   `PUT /api/v1/me/attendances/{showId}` com o `X-CSRF`, vira "Vou ✓" (`aria-pressed="true"`), a contagem sobe e a
 *   mudança é anunciada (`role="status"`) com a nova contagem; tocar de novo desmarca (`DELETE`) e a contagem volta. O
 *   estado inicial vem de `GET /api/v1/me/attendances`.
 * - E2E-02 (parte do botão, Emenda 1) — show cancelado ou adiado: sem o botão, com "{n} vão"; show que já passou: sem
 *   o botão, com "{n} foram".
 * - IT-05 — contagem indisponível (a rota de contagens fora do ar): a página sai com o show e o botão, sem a contagem e
 *   sem aviso de erro.
 * - Privacidade (ADR-0008): a página não tem "Quem vai" nem lista de pessoas.
 */
const now = new Date("2026-10-19T15:00:00Z");
const showId = "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42";
const title = "Noite da Peste II";
const slug = "noite-da-peste-ii-2026-10-24";

type Status = "scheduled" | "cancelled" | "postponed";

function showDetail(
  status: Status = "scheduled",
  startsAt = "2026-10-25T01:00:00Z",
) {
  return {
    id: showId,
    slug,
    title,
    city: { slug: "curitiba", name: "Curitiba", timeZone: "America/Sao_Paulo" },
    startsAt,
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
  };
}

type Call = {
  method: string;
  path: string;
  search: string;
  csrf: string | null;
};

const calls: Call[] = [];

interface ApiState {
  show: ReturnType<typeof showDetail>;
  /** Contagem em `counts` (ou a rota fora do ar). */
  count: number | "down";
  /** O que o fã marcou (ou anônimo: 401). */
  mine: string[] | "anonymous";
}

function stubApi(state: ApiState) {
  return vi
    .spyOn(globalThis, "fetch")
    .mockImplementation(async (input, init) => {
      const request = new Request(input, init);
      const url = new URL(request.url, window.location.href);
      const call = {
        method: request.method.toUpperCase(),
        path: url.pathname,
        search: url.search,
        csrf: request.headers.get("X-CSRF"),
      };
      calls.push(call);
      if (call.path === `/api/v1/cities/curitiba/shows/${slug}`) {
        return Response.json(state.show);
      }
      if (call.path === "/api/v1/attendances/counts") {
        if (state.count === "down") {
          throw new TypeError("fetch failed");
        }
        return Response.json({ counts: { [showId]: state.count } });
      }
      if (call.path === "/api/v1/antiforgery") {
        return Response.json({ token: "csrf-token" });
      }
      if (call.path === "/api/v1/me/attendances") {
        return state.mine === "anonymous"
          ? Response.json(
              { code: "auth.required", status: 401, title: "auth.required" },
              {
                status: 401,
                headers: { "content-type": "application/problem+json" },
              },
            )
          : Response.json({ showIds: state.mine });
      }
      if (call.path === `/api/v1/me/attendances/${showId}`) {
        return new Response(null, { status: 204 });
      }
      throw new TypeError("fetch failed");
    });
}

vi.mock("./show-status", () => ({
  showStatusHeader: "x-subsolo-show",
  showStatus: async () => undefined,
}));

beforeEach(() => {
  calls.length = 0;
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(now);
  vi.stubEnv("API_URL", "http://api.test");
  vi.stubEnv("SUBSOLO_PUBLIC_URL", "https://subsolo.example");
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
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function renderShowPage() {
  const { default: ShowDetailPage } = await import("./page");
  return render(
    await ShowDetailPage({
      params: Promise.resolve({ cidade: "curitiba", show: slug }),
    } as unknown as Parameters<typeof ShowDetailPage>[0]),
  );
}

const attendName = `Vou ao show ${title}`;

/** O texto da contagem ("3 vão", "1 vai", "3 foram"…) na página. */
function countText(container: HTMLElement): string {
  return (
    (container.textContent ?? "").match(
      /\d+\s*(vão|vai|foram|foi)(?![a-zà-ú])/i,
    )?.[0] ?? ""
  );
}

/** O botão aceita o toque (sessão carregada, sem requisição em andamento). */
async function ready(button: HTMLElement) {
  await waitFor(() =>
    expect(
      button.hasAttribute("disabled") ||
        button.getAttribute("aria-disabled") === "true",
    ).toBe(false),
  );
}

describe("Vou na página do show", () => {
  it('SPEC-0029:E2E-01 show agendado: botão "Vou ao show {título}" (Vou) e a contagem "{n} vão"', async () => {
    stubApi({ show: showDetail(), count: 3, mine: [] });

    const { container } = await renderShowPage();

    const button = screen.getByRole("button", { name: attendName });
    expect(button.getAttribute("aria-pressed")).toBe("false");
    expect(button.textContent ?? "").toMatch(/vou/i);
    expect(button.textContent ?? "").not.toMatch(/✓/);
    await waitFor(() => expect(countText(container)).toMatch(/^3\s*vão$/i));
  });

  it("SPEC-0029:E2E-01 o fã marca (PUT com X-CSRF): Vou ✓, contagem +1 anunciada; desmarca (DELETE) e volta", async () => {
    stubApi({ show: showDetail(), count: 3, mine: [] });
    const { container } = await renderShowPage();
    const button = screen.getByRole("button", { name: attendName });
    await ready(button);

    await act(async () => {
      fireEvent.click(button);
    });

    await waitFor(() =>
      expect(button.getAttribute("aria-pressed")).toBe("true"),
    );
    const put = calls.find((call) => call.method === "PUT");
    expect(put?.path).toBe(`/api/v1/me/attendances/${showId}`);
    expect(put?.csrf).toBe("csrf-token");
    expect(button.textContent ?? "").toMatch(/vou\s*✓/i);
    expect(
      screen.getByRole("button", { name: attendName }),
      "o nome acessível mudou com o estado (H27-04)",
    ).toBe(button);
    await waitFor(() => expect(countText(container)).toMatch(/^4\s*vão$/i));
    const status = screen
      .getAllByRole("status")
      .map((region) => region.textContent ?? "")
      .join(" ");
    expect(status, "a mudança e a nova contagem não foram anunciadas").toMatch(
      /4/,
    );

    await ready(button);
    await act(async () => {
      fireEvent.click(button);
    });

    await waitFor(() =>
      expect(button.getAttribute("aria-pressed")).toBe("false"),
    );
    expect(calls.find((call) => call.method === "DELETE")?.path).toBe(
      `/api/v1/me/attendances/${showId}`,
    );
    await waitFor(() => expect(countText(container)).toMatch(/^3\s*vão$/i));
  });

  it("SPEC-0029:E2E-01 quem já marcou vê o botão pressionado (estado de GET /me/attendances)", async () => {
    stubApi({ show: showDetail(), count: 5, mine: [showId] });

    await renderShowPage();

    const button = screen.getByRole("button", { name: attendName });
    await waitFor(() =>
      expect(button.getAttribute("aria-pressed")).toBe("true"),
    );
    expect(calls.some((call) => call.path === "/api/v1/me/attendances")).toBe(
      true,
    );
  });

  it("SPEC-0029:E2E-02 show cancelado: sem o botão Vou, com a contagem", async () => {
    stubApi({ show: showDetail("cancelled"), count: 7, mine: [] });

    const { container } = await renderShowPage();

    await waitFor(() => expect(countText(container)).toMatch(/^7\s*vão$/i));
    expect(screen.queryByRole("button", { name: /vou/i })).toBeNull();
  });

  it('SPEC-0029:E2E-02 show que já passou: sem o botão Vou, com "{n} foram"', async () => {
    stubApi({
      show: showDetail("scheduled", "2026-10-18T01:00:00Z"),
      count: 2,
      mine: [],
    });

    const { container } = await renderShowPage();

    await waitFor(() => expect(countText(container)).toMatch(/^2\s*foram$/i));
    expect(screen.queryByRole("button", { name: /vou/i })).toBeNull();
  });

  it('SPEC-0029:E2E-02 show adiado: sem o botão Vou, com "{n} vão"', async () => {
    stubApi({ show: showDetail("postponed"), count: 4, mine: [] });

    const { container } = await renderShowPage();

    await waitFor(() => expect(countText(container)).toMatch(/^4\s*vão$/i));
    expect(screen.queryByRole("button", { name: /vou/i })).toBeNull();
  });

  it("SPEC-0029:IT-05 com a contagem indisponível, a página sai com o show e o botão, sem a contagem e sem erro", async () => {
    stubApi({ show: showDetail(), count: "down", mine: [] });

    const { container } = await renderShowPage();

    expect(screen.getByRole("heading", { level: 1, name: title })).toBeTruthy();
    expect(screen.getByRole("button", { name: attendName })).toBeTruthy();
    expect(
      calls.some((call) => call.path === "/api/v1/attendances/counts"),
      "a página não pediu a contagem ao Engagement",
    ).toBe(true);
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 20));
    });
    expect(countText(container), "contagem sem fonte").toBe("");
    expect(container.textContent ?? "").not.toMatch(/indispon[ií]vel|erro/i);
  });

  it("SPEC-0029:IT-04 a página não tem lista de quem vai", async () => {
    stubApi({ show: showDetail(), count: 3, mine: [] });

    const { container } = await renderShowPage();

    expect(container.textContent ?? "").not.toMatch(/quem vai/i);
  });
});
