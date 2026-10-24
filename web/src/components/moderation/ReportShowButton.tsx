"use client";

import { useRef, useState } from "react";

import { getReporterSession } from "@/api/reports";

import { reportMessages } from "./messages";
import { ReportShowSheet } from "./ReportShowSheet";

export interface ReportShowButtonProps {
  /** O show (id da API). */
  showId: string;
  /** O título do show. */
  title: string;
  /** A página para onde o anônimo volta depois de entrar. */
  returnTo: string;
}

/** Vai para outra página do site (documento novo). */
function navigate(path: string) {
  window.location.assign(new URL(path, window.location.href).href);
}

/**
 * SPEC-0033 — o "Reportar" da página do show (no rodapé dela, como o "Denunciar" da tela "Página do Show" do Stitch):
 * logado → abre a folha "Reportar show"; anônimo → `/entrar?returnTo=…`. A sessão é conferida no toque
 * (`GET /api/v1/me`), para a página continuar igual para todos. Ao fechar a folha, o foco volta para o botão.
 */
export function ReportShowButton({
  showId,
  title,
  returnTo,
}: ReportShowButtonProps) {
  const button = useRef<HTMLButtonElement>(null);
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");

  async function start() {
    if (busy) {
      return;
    }
    setBusy(true);
    setMessage("");
    const session = await getReporterSession();
    setBusy(false);
    if (session.ok) {
      setOpen(true);
      return;
    }
    if (session.status === 401) {
      navigate(`/entrar?returnTo=${encodeURIComponent(returnTo)}`);
      return;
    }
    setMessage(reportMessages.failed);
  }

  function close() {
    setOpen(false);
    button.current?.focus();
  }

  return (
    <div className="flex flex-col items-center gap-2">
      <button
        ref={button}
        type="button"
        aria-haspopup="dialog"
        aria-disabled={busy}
        onClick={() => void start()}
        className="inline-flex min-h-11 items-center gap-1.5 px-2 font-label text-xs uppercase tracking-wider text-text-muted transition-colors hover:text-text aria-disabled:opacity-50"
      >
        <svg
          viewBox="0 0 24 24"
          width="16"
          height="16"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="square"
          aria-hidden="true"
          focusable="false"
        >
          <path d="M5 21V4h11l-1.5 4L16 12H5" />
        </svg>
        Reportar show falso ou cancelado
      </button>
      <p role="status" className="text-sm text-text empty:hidden">
        {message}
      </p>
      {open && (
        <ReportShowSheet showId={showId} title={title} open onClose={close} />
      )}
    </div>
  );
}
