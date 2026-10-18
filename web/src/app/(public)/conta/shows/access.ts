import type { Me } from "@/api/session";

/** SPEC-0023 — quem cadastra shows: o artista (perfil aprovado) ou o admin. */
export function canManageShows(me: Me): boolean {
  return me.roles.includes("artist") || me.roles.includes("admin");
}

/** SPEC-0023 — o admin escolhe qualquer perfil organizador. */
export function isAdmin(me: Me): boolean {
  return me.roles.includes("admin");
}
