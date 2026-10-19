import type { Metadata } from "next";

import type { ProfileShow, PublicProfile } from "@/api/public-profile";

/** O tipo do perfil como aparece no título e na descrição. */
const typeLabels: Record<string, string> = {
  band: "Banda",
  venue: "Casa",
  producer: "Produtor",
};

/** O primeiro segmento da página do perfil por tipo (`/banda|casa|produtor/{slug}`). */
export const profileSegments: Record<string, string> = {
  band: "banda",
  venue: "casa",
  producer: "produtor",
};

/** Medidas da variante `og` do flyer (ADR-0015). */
const ogImageSize = { width: 1200, height: 630 } as const;

/** O rótulo do tipo do perfil ("Banda", "Casa" ou "Produtor"). */
export function profileTypeLabel(type: string): string {
  return typeLabels[type] ?? "Perfil";
}

/** "{nome} · {Banda|Casa|Produtor} · Subsolo". */
export function profileTitle(profile: PublicProfile): string {
  return `${profile.name} · ${profileTypeLabel(profile.type)} · Subsolo`;
}

/** "{tipo} de {cidade} · {estilos}" (sem estilos, só "{tipo} de {cidade}"). */
export function profileDescription(profile: PublicProfile): string {
  const base = `${profileTypeLabel(profile.type)} de ${profile.city.name}`;
  return profile.styles.length > 0
    ? `${base} · ${profile.styles.map((style) => style.name).join(", ")}`
    : base;
}

/**
 * A URL absoluta da variante `og` do flyer do show (`{publicUrl}/media/flyers/{id}-og.jpg`, nomes do ADR-0015), a
 * partir da miniatura (`/media/flyers/{id}-thumb.webp`); sem flyer, nenhuma.
 */
function ogImageOf(show: ProfileShow, base: string): string | undefined {
  const match = /^\/media\/flyers\/([^/]+)-thumb\.[a-z]+$/.exec(
    show.flyerThumbUrl ?? "",
  );
  return match ? `${base}/media/flyers/${match[1]}-og.jpg` : undefined;
}

/**
 * SPEC-0026 — os metadados da página do perfil (§6): `<title>` "{nome} · {Banda|Casa|Produtor} · Subsolo",
 * `description` "{tipo} de {cidade} · {estilos}" e Open Graph com o flyer `og` do próximo show (Emenda v2, c); sem
 * próximo show, nenhuma imagem.
 */
export function profileMetadata(
  profile: PublicProfile,
  nextShow: ProfileShow | undefined,
  publicUrl: string,
): Metadata {
  const base = publicUrl.replace(/\/+$/, "");
  const title = profileTitle(profile);
  const description = profileDescription(profile);
  const image = nextShow ? ogImageOf(nextShow, base) : undefined;
  const url = `${base}/${profileSegments[profile.type] ?? "banda"}/${profile.slug}`;
  return {
    title: { absolute: title },
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      type: "website",
      siteName: "Subsolo",
      locale: "pt_BR",
      ...(image
        ? {
            images: [
              {
                url: image,
                ...ogImageSize,
                alt: `Flyer de ${nextShow?.title}`,
              },
            ],
          }
        : {}),
    },
    twitter: {
      card: image ? "summary_large_image" : "summary",
      title,
      description,
      ...(image ? { images: [image] } : {}),
    },
  };
}
