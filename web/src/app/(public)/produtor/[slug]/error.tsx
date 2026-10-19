"use client";

import { ProfileUnavailable } from "@/components/profiles/ProfileUnavailable";

/** SPEC-0026 — falha inesperada ao carregar a página do perfil: a mesma mensagem de indisponibilidade. */
export default function ProfileError() {
  return <ProfileUnavailable />;
}
