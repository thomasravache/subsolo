"use client";

import { useEffect, useState } from "react";

import {
  getNotificationPreferences,
  getPushPublicKey,
  savePushSubscription,
  updateNotificationPreferences,
  type NotificationPreferences,
  type PreferenceFlags,
} from "@/api/notifications";
import { buttonClasses } from "@/components/ui/Button";

import { noticeMessages, switches } from "./messages";
import {
  keyBytes,
  notificationPermission,
  pushSupport,
  registration,
  type PushSupport,
} from "./push";

/** A situação do push neste aparelho. */
type DeviceState =
  "checking" | PushSupport | "inactive" | "activating" | "active" | "blocked";

/** Situação inicial a partir do navegador: suporte, permissão e se o aparelho já está inscrito. */
async function initialState(): Promise<DeviceState> {
  const support = pushSupport();
  if (support !== "supported") {
    return support;
  }
  if ((await notificationPermission()) === "granted") {
    try {
      const existing = await (
        await registration()
      ).pushManager.getSubscription();
      return existing ? "active" : "inactive";
    } catch {
      return "inactive";
    }
  }
  return "inactive";
}

/** O texto anunciado (`role="status"`) para a situação. */
function statusText(state: DeviceState, failed: string): string {
  switch (state) {
    case "active":
      return noticeMessages.active;
    case "blocked":
      return noticeMessages.blocked;
    case "unsupported":
      return noticeMessages.unsupported;
    case "ios-install":
      return noticeMessages.installIos;
    default:
      return failed;
  }
}

/**
 * SPEC-0030 — a parte "Avisos" da tela "Avisos e Seguindo" (em `/seguindo`; Stitch 35b282364c47425c9e9cfe89e7b1c1e2,
 * com os ajustes da spec): "Ativar avisos" só pede a permissão depois do toque (ADR-0013); concedida, inscreve o
 * aparelho com a chave pública da API e grava a inscrição; a situação ("Avisos ativos neste aparelho", bloqueados, sem
 * suporte, ou no iPhone sem instalar a instrução da tela de início, no bloco de alerta) é anunciada; e os quatro
 * interruptores (`role="switch"`) salvam as preferências na hora.
 */
export function NoticesPanel({
  initial,
}: {
  initial: NotificationPreferences;
}) {
  const [preferences, setPreferences] = useState(initial);
  const [state, setState] = useState<DeviceState>("checking");
  const [message, setMessage] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let current = true;
    void initialState().then((found) => {
      if (current) {
        setState(found);
      }
    });
    return () => {
      current = false;
    };
  }, []);

  async function activate() {
    if (state === "activating") {
      return;
    }
    setMessage("");
    // ADR-0013: a permissão só é pedida aqui, depois do toque.
    const current = await notificationPermission();
    const permission =
      current === "default" ? await Notification.requestPermission() : current;
    if (permission !== "granted") {
      setState("blocked");
      return;
    }
    setState("activating");
    const publicKey = await getPushPublicKey();
    if (!publicKey.ok) {
      setState("inactive");
      setMessage(noticeMessages.activateFailed);
      return;
    }
    try {
      const subscription = await (
        await registration()
      ).pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: keyBytes(publicKey.value),
      });
      const json = subscription.toJSON();
      const saved = await savePushSubscription({
        endpoint: json.endpoint ?? subscription.endpoint,
        keys: { p256dh: json.keys?.p256dh ?? "", auth: json.keys?.auth ?? "" },
      });
      if (!saved.ok) {
        throw new Error(saved.code);
      }
    } catch {
      setState("inactive");
      setMessage(noticeMessages.activateFailed);
      return;
    }
    setState("active");
    const refreshed = await getNotificationPreferences();
    if (refreshed.ok) {
      setPreferences(refreshed.value);
    }
  }

  async function toggle(key: keyof PreferenceFlags) {
    if (saving) {
      return;
    }
    const previous = preferences;
    const next = { ...preferences, [key]: !preferences[key] };
    setPreferences(next);
    setSaving(true);
    setMessage("");
    const result = await updateNotificationPreferences(next);
    setSaving(false);
    if (result.ok) {
      setPreferences(result.value);
    } else {
      setPreferences(previous);
      setMessage(noticeMessages.saveFailed);
    }
  }

  const canActivate =
    state === "inactive" || state === "blocked" || state === "activating";
  const status = statusText(state, message);

  return (
    <section aria-labelledby="avisos-titulo" className="flex flex-col gap-3">
      <h2
        id="avisos-titulo"
        className="flex items-center gap-2 border-b border-border pb-2 font-display text-xl uppercase leading-none tracking-wide text-text"
      >
        <span aria-hidden="true" className="h-5 w-1 bg-accent" />
        {noticeMessages.title}
      </h2>

      <div className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4">
        <div>
          <h3 className="font-body text-base font-bold text-text">
            {noticeMessages.pushTitle}
          </h3>
          <p className="text-sm text-text-muted">
            {noticeMessages.pushDescription}
          </p>
        </div>
        {state === "ios-install" ? (
          <div
            role="status"
            className="rounded-md border border-warning bg-surface-raised p-3 text-sm text-text"
          >
            <p className="font-bold">{noticeMessages.installIos}</p>
            <p className="text-text-muted">{noticeMessages.installIosHint}</p>
          </div>
        ) : (
          <p
            role="status"
            className={
              state === "active"
                ? "text-sm font-bold text-text"
                : "text-sm text-text-muted"
            }
          >
            {status}
          </p>
        )}
        {state === "blocked" ? (
          <p className="text-sm text-text-muted">
            {noticeMessages.blockedHint}
          </p>
        ) : null}
        {canActivate ? (
          <button
            type="button"
            aria-disabled={state === "activating"}
            onClick={() => void activate()}
            className={buttonClasses(
              "primary",
              "md",
              "w-full aria-disabled:opacity-50",
            )}
          >
            {noticeMessages.activate}
          </button>
        ) : null}
      </div>

      <ul className="flex flex-col divide-y divide-border rounded-md border border-border bg-surface">
        {switches.map((item) => {
          const checked = preferences[item.key];
          return (
            <li key={item.key}>
              <button
                type="button"
                role="switch"
                aria-checked={checked}
                aria-disabled={saving}
                onClick={() => void toggle(item.key)}
                className="flex min-h-11 w-full items-center justify-between gap-3 p-4 text-left font-body text-base text-text aria-disabled:cursor-wait"
              >
                <span>{item.label}</span>
                <span
                  aria-hidden="true"
                  className={`relative inline-flex h-6 w-11 shrink-0 rounded-full border-2 border-transparent transition-colors ${checked ? "bg-accent" : "bg-border"}`}
                >
                  <span
                    className={`inline-block h-5 w-5 rounded-full bg-text-on-accent transition-transform ${checked ? "translate-x-5" : "translate-x-0"}`}
                  />
                </span>
              </button>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
