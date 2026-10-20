"use client";

import Link from "next/link";
import { useState } from "react";

import { setFollowing, type FollowTarget, type MyFollows } from "@/api/follows";
import { buttonClasses } from "@/components/ui/Button";

import { followAnnouncement, followLabel, followMessages } from "./messages";

/** Um item seguido, já com o endereço da página. */
interface Item {
  key: string;
  name: string;
  href: string;
  target: FollowTarget;
}

/** As seções da aba, por tipo (§6): cidades, bandas, casas e produtores. */
const sections = [
  { id: "cidades", title: "Cidades" },
  { id: "bandas", title: "Bandas", type: "band", segment: "banda" },
  { id: "casas", title: "Casas", type: "venue", segment: "casa" },
  {
    id: "produtores",
    title: "Produtores",
    type: "producer",
    segment: "produtor",
  },
] as const;

function itemsOf(
  follows: MyFollows,
  section: (typeof sections)[number],
): Item[] {
  if (!("type" in section)) {
    return follows.cities.map((city) => ({
      key: `city:${city.slug}`,
      name: city.name,
      href: `/${city.slug}`,
      target: { kind: "city", slug: city.slug },
    }));
  }
  return follows.profiles
    .filter((profile) => profile.type === section.type)
    .map((profile) => ({
      key: `profile:${profile.id}`,
      name: profile.name,
      href: `/${section.segment}/${profile.slug}`,
      target: { kind: "profile", id: profile.id },
    }));
}

function without(follows: MyFollows, target: FollowTarget): MyFollows {
  return target.kind === "city"
    ? {
        ...follows,
        cities: follows.cities.filter((city) => city.slug !== target.slug),
      }
    : {
        ...follows,
        profiles: follows.profiles.filter(
          (profile) => profile.id !== target.id,
        ),
      };
}

/**
 * SPEC-0027 — a lista da aba "Seguindo" (parte "Seguindo" da tela "Avisos e Seguindo" do Stitch): uma seção por tipo,
 * com cabeçalho, só as que têm itens; cada item leva à página dele e tem "Deixar de seguir" (some da lista na hora, com
 * a mudança anunciada). Sem nada: o texto do §2.
 */
export function FollowingList({ initial }: { initial: MyFollows }) {
  const [follows, setFollows] = useState(initial);
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState("");

  async function unfollow(item: Item) {
    setBusy(item.key);
    const result = await setFollowing(item.target, false);
    setBusy(null);
    if (result.ok) {
      setFollows((current) => without(current, item.target));
      setMessage(followAnnouncement(item.name, false));
    } else {
      setMessage(followMessages.failed);
    }
  }

  const filled = sections
    .map((section) => ({ ...section, items: itemsOf(follows, section) }))
    .filter((section) => section.items.length > 0);

  return (
    <>
      <p role="status" className="sr-only">
        {message}
      </p>
      {filled.length === 0 ? (
        <p className="rounded-md border border-dashed border-border px-4 py-6 text-center text-lg text-text">
          {followMessages.empty}
        </p>
      ) : (
        filled.map((section) => (
          <section
            key={section.id}
            aria-labelledby={`seguindo-${section.id}`}
            className="flex flex-col gap-3"
          >
            <h2
              id={`seguindo-${section.id}`}
              className="flex items-center gap-2 border-b border-border pb-2 font-display text-xl uppercase leading-none tracking-wide text-text"
            >
              <span aria-hidden="true" className="h-5 w-1 bg-accent" />
              {section.title}
            </h2>
            <ul className="flex flex-col gap-2">
              {section.items.map((item) => (
                <li
                  key={item.key}
                  className="flex items-center justify-between gap-3 rounded-md border border-border bg-surface p-4"
                >
                  <Link
                    href={item.href}
                    className="min-w-0 break-words font-body text-lg font-bold text-text hover:underline"
                  >
                    {item.name}
                  </Link>
                  <button
                    type="button"
                    aria-label={followLabel(item.name, true)}
                    disabled={busy === item.key}
                    onClick={() => void unfollow(item)}
                    className={buttonClasses(
                      "secondary",
                      "md",
                      "shrink-0 text-xs",
                    )}
                  >
                    {followMessages.unfollow}
                  </button>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </>
  );
}
