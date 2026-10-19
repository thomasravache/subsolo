// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

/**
 * Itens herdados da revisão da SPEC-0026 (G4) na rota do perfil (`loadProfile` → `profileRouteMetadata`/`ProfileRoute`),
 * com a API respondendo por um `fetch` falso (o status do `proxy` não veio: o perfil é consultado de verdade):
 * - H26-05 — com a lista de shows indisponível (conexão recusada em `GET /api/v1/shows`), a rota sai com o perfil e o
 *   aviso "Não foi possível carregar os shows agora." (status 200: nem "Perfil indisponível" nem "não encontrado"). Toda
 *   outra chamada à API além do perfil também falha, e a página continua de pé.
 * - H26-03 (parte do "próximo show") — o `og:image` vem do primeiro show não cancelado e ainda não iniciado: um show
 *   cancelado e um que já começou, mesmo no topo de "próximos", não viram a imagem da página.
 */
const now = new Date("2026-10-19T15:00:00Z");
const publicUrl = "https://subsolo.example";

const band = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
  type: "band",
  name: "Podridão",
  slug: "podridao",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [{ slug: "death", name: "Death Metal" }],
  links: { instagram: "https://www.instagram.com/podridao" },
};

function show(
  id: string,
  startsAt: string,
  status: "scheduled" | "cancelled" | "postponed" = "scheduled",
) {
  return {
    id: `3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f4${id}`,
    slug: `noite-${id}-2026-10-19`,
    title: `Noite ${id}`,
    startsAt,
    venueName: "Porão do Rock",
    lineup: ["Podridão"],
    styles: ["death"],
    status,
    flyerThumbUrl: `/media/flyers/flyer${id}-thumb.webp`,
    attendingCount: 0,
    city: {
      slug: "curitiba",
      name: "Curitiba",
      timeZone: "America/Sao_Paulo",
    },
  };
}

vi.mock("@/components/profiles/profile-status", () => ({
  profileStatusHeader: "x-subsolo-profile",
  profileStatus: async () => undefined,
}));

function pathOf(input: Parameters<typeof fetch>[0]): string {
  return new URL(
    typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url,
  ).pathname;
}

/** O perfil `band/podridao` existe; `/api/v1/shows` responde com `shows` (ou falha); o resto da API está fora do ar. */
function stubApi(shows: ReturnType<typeof show>[] | "down") {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input) => {
    const path = pathOf(input);
    if (path === "/api/v1/profiles/band/podridao") {
      return Response.json(band);
    }
    if (path === "/api/v1/shows" && shows !== "down") {
      return Response.json({ shows });
    }
    throw new TypeError("fetch failed");
  });
}

beforeEach(() => {
  vi.useFakeTimers({ toFake: ["Date"] });
  vi.setSystemTime(now);
  vi.stubEnv("API_URL", "http://api.test");
  vi.stubEnv("SUBSOLO_PUBLIC_URL", publicUrl);
});

afterEach(() => {
  vi.useRealTimers();
  vi.restoreAllMocks();
  vi.unstubAllEnvs();
  vi.resetModules();
});

type Loose = Record<string, unknown>;

function ogImageUrls(metadata: Loose): string[] {
  const openGraph = (metadata["openGraph"] ?? {}) as Loose;
  return [openGraph["images"]]
    .flat()
    .filter((image) => image !== undefined && image !== null)
    .map((image) =>
      typeof image === "string" || image instanceof URL
        ? String(image)
        : String((image as Loose)["url"]),
    );
}

describe("rota do perfil", () => {
  it("SPEC-0027:H26-05 com a lista de shows indisponível, a rota sai com o perfil e o aviso", async () => {
    stubApi("down");
    const { ProfileRoute, profileRouteMetadata } =
      await import("@/components/profiles/profile-route");

    const metadata = (await profileRouteMetadata("band", "podridao")) as Loose;
    render(await ProfileRoute({ type: "band", slug: "podridao" }));

    const title = metadata["title"];
    expect(
      typeof title === "string" ? title : (title as Loose)["absolute"],
    ).toBe("Podridão · Banda · Subsolo");
    expect(
      screen.getByRole("heading", { level: 1, name: /podridão/i }),
    ).toBeTruthy();
    expect(
      screen.getAllByText("Não foi possível carregar os shows agora.").length,
    ).toBeGreaterThan(0);
    expect(screen.queryByText("Nenhum show anunciado ainda.")).toBeNull();
    expect(screen.queryByText(/perfil indisponível/i)).toBeNull();
    expect(screen.queryByText(/perfil não encontrado/i)).toBeNull();
  });

  it("SPEC-0027:H26-03 o og:image vem do primeiro show não cancelado e ainda não iniciado", async () => {
    stubApi([
      show("1", "2026-10-19T13:00:00Z", "cancelled"),
      show("2", "2026-10-19T14:00:00Z"),
      show("3", "2026-10-20T23:00:00Z"),
      show("4", "2026-10-21T23:00:00Z"),
    ]);
    const { profileRouteMetadata } =
      await import("@/components/profiles/profile-route");

    const metadata = (await profileRouteMetadata("band", "podridao")) as Loose;

    expect(ogImageUrls(metadata)).toEqual([
      `${publicUrl}/media/flyers/flyer3-og.jpg`,
    ]);
  });

  it("SPEC-0027:H26-03 sem show não cancelado e ainda não iniciado, nenhuma imagem de flyer", async () => {
    stubApi([
      show("1", "2026-10-19T13:00:00Z", "cancelled"),
      show("2", "2026-10-19T14:00:00Z"),
      show("5", "2026-10-25T23:00:00Z", "cancelled"),
    ]);
    const { profileRouteMetadata } =
      await import("@/components/profiles/profile-route");

    const metadata = (await profileRouteMetadata("band", "podridao")) as Loose;

    expect(
      ogImageUrls(metadata).filter((url) => url.includes("/media/flyers/")),
    ).toEqual([]);
  });
});
