"use client";

import {
  useCallback,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
} from "react";

import type { CitySummary } from "@/api/client";
import type { QueueItem } from "@/api/profiles";
import type { ReportQueueItem } from "@/api/reports";

import { ProfileRequestsQueue } from "./ProfileRequestsQueue";
import { ReportsQueue } from "./ReportsQueue";

export interface AdminQueueTabsProps {
  profileRequests: QueueItem[];
  cities: CitySummary[];
  /** A fila de denúncias; `undefined` quando a leitura falhou (só o painel de denúncias avisa, SPEC-0037 H33-03). */
  reports: ReportQueueItem[] | undefined;
}

/** SPEC-0037 (H33-03) — o painel de denúncias sem a fila (a leitura falhou); a aba de pedidos de perfil segue. */
const reportsUnavailable =
  "As denúncias estão indisponíveis por alguns instantes. Tente de novo.";

type Tab = "profiles" | "reports";

const order: Tab[] = ["profiles", "reports"];

/**
 * SPEC-0033 — as abas da "Fila do admin" (tela "Fila do Admin - Subsolo" do Stitch): "PEDIDOS DE PERFIL (n)" (SPEC-0021,
 * a aba inicial) e "DENÚNCIAS (n)", com a contagem de cada fila (na de denúncias, o número de shows). Padrão de abas da
 * WAI-ARIA: as setas trocam a aba; a aba fora de vista fica escondida, sem perder o estado.
 */
export function AdminQueueTabs({
  profileRequests,
  cities,
  reports,
}: AdminQueueTabsProps) {
  const id = useId();
  const [tab, setTab] = useState<Tab>("profiles");
  const [counts, setCounts] = useState<Record<Tab, number>>({
    profiles: profileRequests.length,
    reports: reports?.length ?? 0,
  });
  const tabRefs = useRef<Record<Tab, HTMLButtonElement | null>>({
    profiles: null,
    reports: null,
  });

  const onProfiles = useCallback(
    (count: number) =>
      setCounts((current) => ({ ...current, profiles: count })),
    [],
  );
  const onReports = useCallback(
    (count: number) => setCounts((current) => ({ ...current, reports: count })),
    [],
  );

  const labels: Record<Tab, string> = {
    profiles: `Pedidos de perfil (${counts.profiles})`,
    reports: reports ? `Denúncias (${counts.reports})` : "Denúncias",
  };
  const tabId = (key: Tab) => `${id}-aba-${key}`;
  const panelId = (key: Tab) => `${id}-painel-${key}`;

  function onKeyDown(event: KeyboardEvent<HTMLButtonElement>) {
    if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") {
      return;
    }
    event.preventDefault();
    const step = event.key === "ArrowRight" ? 1 : -1;
    const next =
      order[(order.indexOf(tab) + step + order.length) % order.length]!;
    setTab(next);
    tabRefs.current[next]?.focus();
  }

  return (
    <div className="flex flex-col gap-4">
      <div
        role="tablist"
        aria-label="Fila do admin"
        className="flex gap-2 border-b border-border"
      >
        {order.map((key) => (
          <button
            key={key}
            ref={(element) => {
              tabRefs.current[key] = element;
            }}
            id={tabId(key)}
            type="button"
            role="tab"
            aria-selected={tab === key}
            aria-controls={panelId(key)}
            tabIndex={tab === key ? 0 : -1}
            onClick={() => setTab(key)}
            onKeyDown={onKeyDown}
            className={`-mb-px inline-flex min-h-11 items-center border-b-2 px-3 font-label text-xs font-bold uppercase tracking-wider transition-colors ${
              tab === key
                ? "border-accent text-text"
                : "border-transparent text-text-muted hover:text-text"
            }`}
          >
            {labels[key]}
          </button>
        ))}
      </div>
      <div
        id={panelId("profiles")}
        role="tabpanel"
        aria-labelledby={tabId("profiles")}
        hidden={tab !== "profiles"}
      >
        <ProfileRequestsQueue
          initialItems={profileRequests}
          cities={cities}
          onCountChange={onProfiles}
        />
      </div>
      <div
        id={panelId("reports")}
        role="tabpanel"
        aria-labelledby={tabId("reports")}
        hidden={tab !== "reports"}
      >
        {reports ? (
          <ReportsQueue initialItems={reports} onCountChange={onReports} />
        ) : (
          <p role="alert" className="text-base text-text-muted">
            {reportsUnavailable}
          </p>
        )}
      </div>
    </div>
  );
}
