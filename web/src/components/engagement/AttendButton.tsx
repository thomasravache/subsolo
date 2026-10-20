"use client";

import { useEffect, useState } from "react";

import { getMyAttendances, setAttending } from "@/api/attendances";
import { buttonClasses } from "@/components/ui/Button";

import { CheckIcon, FlameIcon } from "./icons";
import {
  attendAnnouncement,
  attendLabel,
  attendMessages,
  goingCount,
  signInHref,
} from "./messages";

/** Sessão vista pelo botão: carregando, sem sessão, logado ou desconhecida (API fora do ar). */
type Session = "loading" | "anonymous" | "signed-in" | "unknown";

export interface AttendButtonProps {
  /** O show (id da API). */
  showId: string;
  /** O título do show (no nome acessível e no anúncio). */
  title: string;
  /** A contagem pública "{n} vão"; sem ela (contagem indisponível), nenhuma contagem. */
  count?: number;
  /** A página para onde o anônimo volta depois de entrar. */
  returnTo: string;
}

/** Vai para outra página do site (documento novo). */
function navigate(path: string) {
  window.location.assign(new URL(path, window.location.href).href);
}

/**
 * SPEC-0029 — botão "Vou" / "Vou ✓" da página do show (§6, tela "Página do Show - Noite da Peste II" do Stitch: "VOU"
 * vermelho com a chama; marcado, contorno vermelho com o visto): o
 * estado vem de `GET /api/v1/me/attendances` pelo navegador (a página fica igual para todos); nome acessível constante
 * "Vou ao show {título}" com `aria-pressed` (H27-04); carregando ou com a requisição em andamento, `aria-disabled` e
 * cliques ignorados, sem perder o foco (H27-02); a contagem "{n} vão" muda na hora e a mudança é anunciada, com a nova
 * contagem, numa região `role="status"`. Anônimo que toca vai para `/entrar?returnTo=…`. Só a contagem é pública
 * (ADR-0008): nada de "Quem vai".
 */
export function AttendButton({
  showId,
  title,
  count: initialCount,
  returnTo,
}: AttendButtonProps) {
  const [session, setSession] = useState<Session>("loading");
  const [attending, setAttendingState] = useState(false);
  const [count, setCount] = useState(initialCount);
  const [shownCount, setShownCount] = useState(initialCount);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  // A contagem do servidor mudou (nova renderização da página) → mostra a nova.
  if (initialCount !== shownCount) {
    setShownCount(initialCount);
    setCount(initialCount);
  }

  useEffect(() => {
    const controller = new AbortController();
    void getMyAttendances(controller.signal).then((result) => {
      if (controller.signal.aborted) {
        return;
      }
      if (result.ok) {
        setAttendingState(result.value.showIds.includes(showId));
        setSession("signed-in");
      } else {
        setSession(result.status === 401 ? "anonymous" : "unknown");
      }
    });
    return () => controller.abort();
  }, [showId]);

  const inactive = session === "loading" || busy;

  async function toggle() {
    if (inactive) {
      return;
    }
    if (session === "anonymous") {
      navigate(signInHref(returnTo));
      return;
    }
    const next = !attending;
    setBusy(true);
    const result = await setAttending(showId, next);
    setBusy(false);
    if (result.ok) {
      const updated =
        count === undefined ? count : Math.max(0, count + (next ? 1 : -1));
      setSession("signed-in");
      setAttendingState(next);
      setCount(updated);
      setMessage(attendAnnouncement(title, next, updated));
    } else if (result.status === 401) {
      navigate(signInHref(returnTo));
    } else {
      setMessage(attendMessages.failed);
    }
  }

  return (
    <div className="flex flex-wrap items-center gap-3">
      <button
        type="button"
        aria-pressed={attending}
        aria-label={attendLabel(title)}
        aria-disabled={inactive}
        onClick={() => void toggle()}
        className={buttonClasses(
          attending ? "secondary" : "primary",
          "lg",
          `min-w-32 aria-disabled:opacity-50 ${attending ? "border-accent text-accent hover:border-accent" : ""}`,
        )}
      >
        {attending ? <CheckIcon /> : <FlameIcon />}
        {attending ? attendMessages.attending : attendMessages.attend}
      </button>
      {count !== undefined && (
        <p className="font-label text-sm font-bold uppercase tracking-wider text-text-muted">
          {goingCount(count)}
        </p>
      )}
      <p role="status" className="sr-only">
        {message}
      </p>
    </div>
  );
}
