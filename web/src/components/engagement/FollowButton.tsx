"use client";

import { useEffect, useState } from "react";

import {
  getMyFollows,
  isFollowing,
  setFollowing,
  type FollowTarget,
} from "@/api/follows";
import { buttonClasses } from "@/components/ui/Button";

import { CheckIcon } from "./icons";
import {
  followAnnouncement,
  followerCount,
  followLabel,
  followMessages,
  signInHref,
} from "./messages";

/** Sessão vista pelo botão: carregando, sem sessão, logado ou desconhecida (API fora do ar). */
type Session = "loading" | "anonymous" | "signed-in" | "unknown";

export interface FollowButtonProps {
  /** A cidade ou o perfil seguido. */
  target: FollowTarget;
  /** O nome do alvo (no nome acessível e no anúncio). */
  name: string;
  /** Texto visível quando não segue (padrão "Seguir"; na agenda, "Seguir {cidade}"). */
  label?: string;
  /** A contagem pública de seguidores, mostrada ao lado ("{n} seguidores"); sem ela, nenhuma contagem. */
  followers?: number;
  /** A página para onde o anônimo volta depois de entrar. */
  returnTo: string;
}

/** Vai para outra página do site (documento novo). */
function navigate(path: string) {
  window.location.assign(new URL(path, window.location.href).href);
}

/**
 * SPEC-0027 — botão "Seguir" / "Seguindo" (§6): o estado vem de `GET /api/v1/me/follows` pelo navegador (a página fica
 * igual para todos e pode ser cacheada); `aria-pressed` e o nome completo ("Seguir {nome}" / "Deixar de seguir
 * {nome}"), a mudança anunciada numa região `role="status"` e a contagem (quando houver) atualizada na hora. Anônimo
 * que toca vai para `/entrar?returnTo=…`.
 */
export function FollowButton({
  target,
  name,
  label = followMessages.follow,
  followers,
  returnTo,
}: FollowButtonProps) {
  const [session, setSession] = useState<Session>("loading");
  const [following, setFollowingState] = useState(false);
  const [count, setCount] = useState(followers);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  useEffect(() => {
    const controller = new AbortController();
    void getMyFollows(controller.signal).then((result) => {
      if (controller.signal.aborted) {
        return;
      }
      if (result.ok) {
        setFollowingState(isFollowing(result.value, target));
        setSession("signed-in");
      } else {
        setSession(result.status === 401 ? "anonymous" : "unknown");
      }
    });
    return () => controller.abort();
  }, [target]);

  async function toggle() {
    if (session === "anonymous") {
      navigate(signInHref(returnTo));
      return;
    }
    const next = !following;
    setBusy(true);
    const result = await setFollowing(target, next);
    setBusy(false);
    if (result.ok) {
      setSession("signed-in");
      setFollowingState(next);
      setCount((current) =>
        current === undefined
          ? current
          : Math.max(0, current + (next ? 1 : -1)),
      );
      setMessage(followAnnouncement(name, next));
    } else if (result.status === 401) {
      navigate(signInHref(returnTo));
    } else {
      setMessage(followMessages.failed);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        aria-pressed={following}
        aria-label={followLabel(name, following)}
        disabled={session === "loading" || busy}
        onClick={() => void toggle()}
        className={buttonClasses(
          following ? "primary" : "secondary",
          "md",
          following ? "" : "border-text-muted",
        )}
      >
        {following ? (
          <>
            <CheckIcon />
            {followMessages.following}
          </>
        ) : (
          label
        )}
      </button>
      {count !== undefined && (
        <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
          {followerCount(count)}
        </p>
      )}
      <p role="status" className="sr-only">
        {message}
      </p>
    </div>
  );
}
