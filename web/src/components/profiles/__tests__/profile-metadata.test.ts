// @vitest-environment node
import { describe, expect, it } from "vitest";

import type { ProfileShow, PublicProfile } from "@/api/public-profile";
import { profileMetadata } from "@/components/profiles/profile-metadata";

/**
 * SPEC-0026 UT-01 — os metadados da página do perfil (§6), montados a partir do perfil e do próximo show:
 * - `<title>` "{nome} · {Banda|Casa|Produtor} · Subsolo";
 * - `description` "{tipo} de {cidade} · {estilos}" (sem estilos, só "{tipo} de {cidade}");
 * - Open Graph com `og:title` e `og:description`; `og:image` = a variante `og` (absoluta pelo `App:PublicUrl`) do
 *   flyer do próximo show; sem próximo show, nenhuma imagem de flyer (vale a padrão do site).
 */
const publicUrl = "https://subsolo.example";

const band: PublicProfile = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f42",
  type: "band",
  name: "Podridão",
  slug: "podridao",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [
    { slug: "death", name: "Death Metal" },
    { slug: "grind", name: "Grindcore" },
  ],
  links: { instagram: "https://www.instagram.com/podridao" },
};

const venue: PublicProfile = {
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f43",
  type: "venue",
  name: "Porão do Rock",
  slug: "porao-do-rock",
  city: { slug: "curitiba", name: "Curitiba" },
  styles: [],
  links: {},
};

const producer: PublicProfile = {
  ...venue,
  id: "0199f0c1-6a4d-7e7f-9b21-8d5c0e9a1f44",
  type: "producer",
  name: "Peste Produções",
  slug: "peste-producoes",
};

// SPEC-0031 (H26-03b): o item traz o `flyerId` (aditivo no contrato); o cast cobre o tipo gerado antes do snapshot novo.
const nextShow = {
  id: "3f1c2b8e-6a4d-4e7f-9b21-8d5c0e9a1f42",
  slug: "noite-da-peste-ii-2026-11-14",
  title: "Noite da Peste II",
  startsAt: "2026-11-15T00:00:00Z",
  venueName: "Porão do Rock",
  lineup: ["Podridão", "Cova Rasa"],
  styles: ["death"],
  status: "scheduled",
  flyerThumbUrl: "/media/flyers/abc-thumb.webp",
  flyerId: "abc",
  attendingCount: 0,
} as ProfileShow;

type Loose = Record<string, unknown>;

function titleOf(metadata: Loose): string {
  const title = metadata["title"];
  return typeof title === "string"
    ? title
    : String((title as Loose | undefined)?.["absolute"] ?? "");
}

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

describe("metadados da página do perfil", () => {
  it("SPEC-0026:UT-01 banda com estilos e próximo show: título, descrição e og:image do flyer do show", () => {
    const metadata = profileMetadata(band, nextShow, publicUrl) as Loose;

    expect(titleOf(metadata)).toBe("Podridão · Banda · Subsolo");
    const description = String(metadata["description"] ?? "");
    expect(description.startsWith("Banda de Curitiba · ")).toBe(true);
    expect(description).toContain("Death Metal");
    expect(description).toContain("Grindcore");
    const openGraph = (metadata["openGraph"] ?? {}) as Loose;
    expect(String(openGraph["title"] ?? "")).toContain("Podridão");
    expect(openGraph["description"]).toBe(description);
    expect(ogImageUrls(metadata)).toEqual([
      `${publicUrl}/media/flyers/abc-og.jpg`,
    ]);
  });

  it("SPEC-0026:UT-01 a imagem do próximo show é absoluta mesmo com barra no fim do App:PublicUrl", () => {
    const metadata = profileMetadata(band, nextShow, `${publicUrl}/`) as Loose;

    expect(ogImageUrls(metadata)).toEqual([
      `${publicUrl}/media/flyers/abc-og.jpg`,
    ]);
  });

  it("SPEC-0026:UT-01 casa sem próximo show: título, descrição sem estilos e nenhuma imagem de flyer (vale a padrão)", () => {
    const metadata = profileMetadata(venue, undefined, publicUrl) as Loose;

    expect(titleOf(metadata)).toBe("Porão do Rock · Casa · Subsolo");
    const description = String(metadata["description"] ?? "");
    expect(description).toBe("Casa de Curitiba");
    const openGraph = (metadata["openGraph"] ?? {}) as Loose;
    expect(String(openGraph["title"] ?? "")).toContain("Porão do Rock");
    expect(openGraph["description"]).toBe(description);
    expect(
      ogImageUrls(metadata).filter((url) => url.includes("/media/flyers/")),
    ).toEqual([]);
  });

  it("SPEC-0026:UT-01 produtor: título com o tipo Produtor", () => {
    const metadata = profileMetadata(producer, undefined, publicUrl) as Loose;

    expect(titleOf(metadata)).toBe("Peste Produções · Produtor · Subsolo");
    expect(String(metadata["description"] ?? "")).toBe("Produtor de Curitiba");
  });
});

/**
 * SPEC-0031 H26-03b — a imagem Open Graph vem do `flyerId` do show (`{base}/media/flyers/{flyerId}-og.jpg`), não de uma
 * expressão regular sobre `flyerThumbUrl`; show sem flyer (`flyerId` nulo) → nenhuma imagem de flyer.
 */
describe("metadados da página do perfil: imagem pelo flyerId", () => {
  const flyerId =
    "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08";

  it("SPEC-0031:H26-03b a og:image é montada pelo flyerId do show, mesmo com outra miniatura", () => {
    const show = {
      ...nextShow,
      flyerId,
      flyerThumbUrl: "/media/flyers/outro-thumb.webp",
    } as ProfileShow;

    const metadata = profileMetadata(band, show, publicUrl) as Loose;

    expect(ogImageUrls(metadata)).toEqual([
      `${publicUrl}/media/flyers/${flyerId}-og.jpg`,
    ]);
  });

  it("SPEC-0031:H26-03b com o flyerId e sem miniatura, a og:image continua", () => {
    const show = { ...nextShow, flyerId, flyerThumbUrl: null } as ProfileShow;

    const metadata = profileMetadata(band, show, `${publicUrl}/`) as Loose;

    expect(ogImageUrls(metadata)).toEqual([
      `${publicUrl}/media/flyers/${flyerId}-og.jpg`,
    ]);
  });

  it("SPEC-0031:H26-03b show sem flyer (flyerId nulo) não tem imagem de flyer, mesmo com uma miniatura", () => {
    const show = {
      ...nextShow,
      flyerId: null,
      flyerThumbUrl: "/media/flyers/abc-thumb.webp",
    } as ProfileShow;

    const metadata = profileMetadata(band, show, publicUrl) as Loose;

    expect(
      ogImageUrls(metadata).filter((url) => url.includes("/media/flyers/")),
    ).toEqual([]);
  });
});
