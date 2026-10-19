// @vitest-environment node
import { describe, expect, it } from "vitest";

import type { ShowDetail } from "@/api/show-detail";
import { showMetadata } from "@/components/shows/show-metadata";

/**
 * SPEC-0024 UT-01 — os metadados da página `/{cidade}/{show}` (§6), montados a partir do detalhe do show:
 * - `<title>` "{título} · {cidade} · Subsolo"; cancelado com o selo na frente ("Cancelado — {título} · …");
 * - `description` "{dia} {hora} · {casa} · {line-up}" (data e hora no fuso da cidade, line-up separado por " / ");
 * - Open Graph: `og:title`, `og:description`, `og:image` = `{App:PublicUrl}{ogUrl}` com 1200×630, `og:type` website e
 *   `og:url` canônica; `twitter:card` summary_large_image; `link rel=canonical`.
 */

const publicUrl = "https://subsolo.example";

/** 2026-11-15T00:00Z = sábado, 14/11 às 21h em São Paulo (UTC−3). */
const show: ShowDetail = {
  id: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
  slug: "noite-da-peste-ii-2026-11-14",
  title: "Noite da Peste II",
  city: { slug: "curitiba", name: "Curitiba", timeZone: "America/Sao_Paulo" },
  startsAt: "2026-11-15T00:00:00Z",
  venue: { name: "Porão do Rock", profileSlug: null },
  lineup: [
    { name: "Cova Rasa", profileSlug: null },
    { name: "Necrocorpo", profileSlug: null },
  ],
  styles: [{ slug: "death", name: "Death" }],
  price: "R$ 30 / R$ 40 na hora",
  ticketUrl: "https://ingressos.example/noite-da-peste-ii",
  status: "scheduled",
  flyer: {
    thumbUrl: "/media/flyers/abc-thumb.webp",
    fullUrl: "/media/flyers/abc-full.webp",
    ogUrl: "/media/flyers/abc-og.jpg",
    width: 1080,
    height: 1350,
  },
};

const canonical = `${publicUrl}/curitiba/noite-da-peste-ii-2026-11-14`;

type Loose = Record<string, unknown>;

/** O `title` como string (o Next aceita string ou `{ absolute }`). */
function titleOf(metadata: Loose): string {
  const title = metadata["title"];
  return typeof title === "string"
    ? title
    : String((title as Loose | undefined)?.["absolute"] ?? "");
}

/** As imagens do Open Graph como lista de `{url, width, height}`. */
function ogImages(metadata: Loose) {
  const openGraph = (metadata["openGraph"] ?? {}) as Loose;
  return [openGraph["images"]]
    .flat()
    .filter((image) => image !== undefined)
    .map((image) =>
      typeof image === "string" || image instanceof URL
        ? { url: String(image) }
        : {
            ...(image as Loose),
            url: String((image as Loose)["url"]),
          },
    );
}

describe("metadados da página do show", () => {
  it("SPEC-0024:UT-01 title, description, Open Graph, twitter:card e canonical como no §6", () => {
    const metadata = showMetadata(show, publicUrl) as Loose;

    expect(titleOf(metadata)).toBe("Noite da Peste II · Curitiba · Subsolo");

    const description = String(metadata["description"] ?? "");
    expect(description).toMatch(
      /^.+ · Porão do Rock · Cova Rasa \/ Necrocorpo$/,
    );
    expect(description, "dia local (14, não 15 em UTC)").toMatch(/\b14\b/);
    expect(description, "hora local (21h, não 00h em UTC)").toMatch(
      /\b21(h|:00)/,
    );

    const openGraph = (metadata["openGraph"] ?? {}) as Loose;
    expect(String(openGraph["title"] ?? "")).toContain("Noite da Peste II");
    expect(openGraph["description"]).toBe(description);
    expect(openGraph["type"]).toBe("website");
    expect(String(openGraph["url"] ?? "")).toBe(canonical);
    expect(ogImages(metadata)).toEqual([
      expect.objectContaining({
        url: `${publicUrl}/media/flyers/abc-og.jpg`,
        width: 1200,
        height: 630,
      }),
    ]);

    const twitter = (metadata["twitter"] ?? {}) as Loose;
    expect(twitter["card"]).toBe("summary_large_image");

    const alternates = (metadata["alternates"] ?? {}) as Loose;
    expect(String(alternates["canonical"] ?? "")).toBe(canonical);
  });

  it("SPEC-0024:UT-01 a imagem do Open Graph é absoluta mesmo com barra no fim do App:PublicUrl", () => {
    const metadata = showMetadata(show, `${publicUrl}/`) as Loose;

    expect(ogImages(metadata).map((image) => image.url)).toEqual([
      `${publicUrl}/media/flyers/abc-og.jpg`,
    ]);
    expect(
      String(((metadata["alternates"] ?? {}) as Loose)["canonical"] ?? ""),
    ).toBe(canonical);
  });

  it("SPEC-0024:UT-01 show cancelado leva o selo no título", () => {
    const metadata = showMetadata(
      { ...show, status: "cancelled" },
      publicUrl,
    ) as Loose;

    expect(titleOf(metadata)).toBe(
      "Cancelado — Noite da Peste II · Curitiba · Subsolo",
    );
  });
});
