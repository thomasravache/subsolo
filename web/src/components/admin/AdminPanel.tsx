"use client";

import { useState } from "react";

import type { AdminCity, Style } from "@/api/admin";

import { CitiesPanel } from "./CitiesPanel";
import { StylesPanel } from "./StylesPanel";

export interface AdminPanelProps {
  cities: AdminCity[];
  styles: Style[];
}

/**
 * SPEC-0017 — conteúdo de `/admin/cidades-e-estilos`: as colunas CIDADES e ESTILOS (lado a lado no desktop, uma sob a
 * outra no celular) e a região `aria-live` que anuncia o sucesso ("Cidade criada.", "Estilo adicionado."…).
 */
export function AdminPanel({ cities, styles }: AdminPanelProps) {
  const [message, setMessage] = useState("");
  return (
    <div className="flex flex-col gap-6">
      <p
        role="status"
        aria-live="polite"
        className="min-h-6 font-label text-sm font-bold uppercase tracking-wider text-text"
      >
        {message}
      </p>
      <div className="grid gap-10 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)]">
        <CitiesPanel initialCities={cities} onSuccess={setMessage} />
        <StylesPanel initialStyles={styles} onSuccess={setMessage} />
      </div>
    </div>
  );
}
