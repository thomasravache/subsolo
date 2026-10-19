import Link from "next/link";
import type { ReactNode } from "react";

import type { ProfileShowItem, PublicProfile } from "@/api/public-profile";
import { FollowButton } from "@/components/engagement/FollowButton";
import { cityTimeParts } from "@/components/shows/city-time";
import { Badge } from "@/components/ui/Badge";
import { Card } from "@/components/ui/Card";
import { InstagramIcon } from "@/components/ui/icons";
import { mainContentId } from "@/components/ui/shell";

import { profileSegments, profileTypeLabel } from "./profile-metadata";
import { BookIcon, HeadphonesIcon, PlaceIcon } from "./profile-icons";

/** Textos da página do perfil (§6). */
export const profileMessages = {
  upcoming: "PRÓXIMOS SHOWS",
  past: "SHOWS PASSADOS",
  noUpcoming: "Nenhum show anunciado ainda.",
  noPast: "Nenhum show passado.",
  showsUnavailable: "Não foi possível carregar os shows agora.",
} as const;

/**
 * SPEC-0026 — a página pública do perfil (§6): nome, cidade, estilos, links das redes, "PRÓXIMOS SHOWS" e "SHOWS
 * PASSADOS". `upcoming`/`past` = `null` quando o endpoint de shows falhou ("Não foi possível carregar os shows agora.").
 * Tela do Stitch "Perfil da Banda - Podridão - Subsolo", usada também para casa e produtor, com os ajustes do §6: sem
 * foto (o topo é a tipografia sobre fundo escuro) e sem o botão "Vou" nos cards (a contagem "X vão" do `Card` fica).
 * SPEC-0027 — "Seguir" / "Seguindo" e a contagem "{n} seguidores" (sem a contagem quando ela não veio).
 */
export interface ProfilePageProps {
  profile: PublicProfile;
  upcoming: ProfileShowItem[] | null;
  past: ProfileShowItem[] | null;
  /** SPEC-0027 — quantos seguem o perfil; `undefined` = indisponível. */
  followers?: number;
}

/** Os links das redes do perfil, na ordem do Stitch, só os informados. */
function networks(profile: PublicProfile) {
  return [
    { name: "Instagram", href: profile.links.instagram, Icon: InstagramIcon },
    { name: "Bandcamp", href: profile.links.bandcamp, Icon: HeadphonesIcon },
    {
      name: "Metal Archives",
      href: profile.links.metalArchives,
      Icon: BookIcon,
    },
  ].filter(
    (network): network is typeof network & { href: string } =>
      typeof network.href === "string" && network.href.length > 0,
  );
}

/** O endereço da página do show na cidade dele. */
function showHref(show: ProfileShowItem): string {
  return `/${show.city.slug}/${show.slug}`;
}

/** Título de seção no estilo do Stitch: barra de destaque, rótulo em maiúsculas e o complemento à direita. */
function SectionHeader({
  id,
  title,
  aside,
}: {
  id: string;
  title: string;
  aside?: string;
}) {
  return (
    <div className="flex items-baseline justify-between gap-2 border-b border-border pb-2">
      <h2
        id={id}
        className="flex items-center gap-2 font-display text-xl uppercase leading-none tracking-wide text-text"
      >
        <span aria-hidden="true" className="h-5 w-1 bg-accent" />
        {title}
      </h2>
      {aside && (
        <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
          {aside}
        </p>
      )}
    </div>
  );
}

/** Aviso no lugar de uma lista (vazia ou sem a API de shows). */
function Notice({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-md border border-dashed border-border px-4 py-6 text-center text-lg text-text">
      {children}
    </p>
  );
}

/** "1 show" ou "N shows". */
function showCount(count: number): string {
  return `${count} ${count === 1 ? "show" : "shows"}`;
}

export function ProfilePage({
  profile,
  upcoming,
  past,
  followers,
}: ProfilePageProps) {
  const links = networks(profile);
  // Ids únicos por perfil: a navegação no cliente mantém a página anterior escondida no DOM (Activity, cacheComponents).
  const ids = {
    upcoming: `proximos-${profile.id}`,
    past: `passados-${profile.id}`,
  };
  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-8 px-4 py-6 md:py-8"
    >
      <header className="flex flex-col gap-4">
        <div className="relative flex min-h-40 flex-col justify-end gap-2 overflow-hidden rounded-md border border-border bg-surface p-4 md:min-h-48 md:p-6">
          <p className="flex items-center gap-1.5 font-label text-xs font-bold uppercase tracking-wider text-text-muted">
            <PlaceIcon className="shrink-0 text-accent" />
            {profileTypeLabel(profile.type)} · {profile.city.name}
          </p>
          <h1 className="break-words font-display text-4xl uppercase leading-none tracking-wide text-text md:text-6xl">
            {profile.name}
          </h1>
        </div>

        <FollowButton
          target={{ kind: "profile", id: profile.id }}
          name={profile.name}
          followers={followers}
          returnTo={`/${profileSegments[profile.type] ?? "banda"}/${profile.slug}`}
        />

        {profile.styles.length > 0 && (
          <ul className="flex flex-wrap gap-1.5" aria-label="Estilos">
            {profile.styles.map((style) => (
              <li key={style.slug}>
                <Badge kind="style">{style.name}</Badge>
              </li>
            ))}
          </ul>
        )}

        {links.length > 0 && (
          <ul className="grid grid-cols-1 gap-2 sm:grid-cols-3">
            {links.map(({ name, href, Icon }) => (
              <li key={name}>
                <a
                  href={href}
                  rel="noopener noreferrer"
                  aria-label={`${name} de ${profile.name}`}
                  className="flex min-h-11 items-center justify-center gap-2 rounded-md border border-border bg-surface px-3 py-2.5 font-label text-xs font-bold uppercase tracking-wider text-text transition-colors hover:border-accent focus-visible:border-accent"
                >
                  <Icon className="shrink-0 text-accent" />
                  {name}
                </a>
              </li>
            ))}
          </ul>
        )}
      </header>

      <section aria-labelledby={ids.upcoming} className="flex flex-col gap-3">
        <SectionHeader
          id={ids.upcoming}
          title={profileMessages.upcoming}
          aside={upcoming ? showCount(upcoming.length) : undefined}
        />
        {upcoming === null ? (
          <Notice>{profileMessages.showsUnavailable}</Notice>
        ) : upcoming.length === 0 ? (
          <Notice>{profileMessages.noUpcoming}</Notice>
        ) : (
          <ul className="grid gap-3.5 md:grid-cols-2 xl:grid-cols-3">
            {upcoming.map((show) => (
              <li key={show.id}>
                <Card
                  href={showHref(show)}
                  flyerSrc={show.flyerThumbUrl}
                  title={show.title}
                  date={show.startsAt}
                  timeZone={show.city.timeZone}
                  venue={`${show.venueName} · ${show.city.name}`}
                  lineup={show.lineup}
                  styles={show.styles}
                  status={show.status}
                  attendingCount={show.attendingCount}
                />
              </li>
            ))}
          </ul>
        )}
      </section>

      <section aria-labelledby={ids.past} className="flex flex-col gap-3">
        <SectionHeader id={ids.past} title={profileMessages.past} />
        {past === null ? (
          <Notice>{profileMessages.showsUnavailable}</Notice>
        ) : past.length === 0 ? (
          <Notice>{profileMessages.noPast}</Notice>
        ) : (
          <ul className="flex flex-col gap-2">
            {past.map((show) => {
              const when = cityTimeParts(show.startsAt, show.city.timeZone);
              return (
                <li key={show.id}>
                  <Link
                    href={showHref(show)}
                    className="flex flex-col gap-0.5 rounded-md border border-border bg-surface p-3 transition-colors hover:border-text-muted focus-visible:border-text-muted"
                  >
                    <span className="font-label text-[11px] font-bold uppercase tracking-wider text-text-muted">
                      <time dateTime={show.startsAt}>
                        {when.day} {when.month} {when.year}
                      </time>{" "}
                      · {show.city.name}
                    </span>
                    <span className="font-display text-base uppercase leading-tight text-text">
                      {show.title}
                    </span>
                  </Link>
                </li>
              );
            })}
          </ul>
        )}
      </section>
    </main>
  );
}
