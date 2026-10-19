import type { Metadata } from "next";
import { cache } from "react";

import {
  getProfileShows,
  getPublicProfile,
  type ProfileShowItem,
  type PublicProfile,
  type PublicProfileType,
} from "@/api/public-profile";
import { publicUrl } from "@/app/(public)/[cidade]/[show]/public-url";

import { ProfileNotFound } from "./ProfileNotFound";
import { ProfilePage } from "./ProfilePage";
import { ProfileUnavailable } from "./ProfileUnavailable";
import { profileMetadata } from "./profile-metadata";
import { profileStatus } from "./profile-status";

/** O perfil da requisição e os shows dele (`null` = endpoint de shows fora: a página mostra o aviso). */
type ProfileView =
  | {
      kind: "ok";
      profile: PublicProfile;
      upcoming: ProfileShowItem[] | null;
      past: ProfileShowItem[] | null;
    }
  | { kind: "not-found" }
  | { kind: "unavailable" };

/**
 * SPEC-0026 — o perfil da requisição (uma consulta só para os metadados e a página). O status HTTP já veio do `proxy`
 * (ADR-0016): com 404 ou 503 ele avisa pelo cabeçalho interno e a API nem é consultada de novo. Os próximos e os
 * passados são pedidos juntos; se o endpoint de shows falhar, a página sai com o perfil e o aviso (status 200).
 */
const loadProfile = cache(
  async (type: PublicProfileType, slug: string): Promise<ProfileView> => {
    const status = await profileStatus();
    if (status) {
      return { kind: status };
    }
    const result = await getPublicProfile(type, slug);
    if (result.kind !== "ok") {
      return result;
    }
    const [upcoming, past] = await Promise.all([
      getProfileShows(result.profile.id, "upcoming"),
      getProfileShows(result.profile.id, "past"),
    ]);
    return {
      kind: "ok",
      profile: result.profile,
      upcoming: upcoming.kind === "ok" ? upcoming.shows : null,
      past: past.kind === "ok" ? past.shows : null,
    };
  },
);

/** Os metadados de `/banda|casa|produtor/{slug}` (§6), ou os do aviso. */
export async function profileRouteMetadata(
  type: PublicProfileType,
  slug: string,
): Promise<Metadata> {
  const view = await loadProfile(type, slug);
  switch (view.kind) {
    case "ok":
      return profileMetadata(view.profile, view.upcoming?.[0], publicUrl());
    case "not-found":
      return {
        title: "Perfil não encontrado · Subsolo",
        robots: { index: false },
      };
    case "unavailable":
      return { title: "Perfil indisponível · Subsolo" };
  }
}

/**
 * SPEC-0026 — a página `/banda|casa|produtor/{slug}`: o perfil com os shows, "Perfil não encontrado" (404) ou "Perfil
 * indisponível" (503). Renderiza o aviso sem `notFound()`, como a página do show.
 */
export async function ProfileRoute({
  type,
  slug,
}: {
  type: PublicProfileType;
  slug: string;
}) {
  const view = await loadProfile(type, slug);
  switch (view.kind) {
    case "ok":
      return (
        <ProfilePage
          profile={view.profile}
          upcoming={view.upcoming}
          past={view.past}
        />
      );
    case "not-found":
      return <ProfileNotFound />;
    case "unavailable":
      return <ProfileUnavailable />;
  }
}
