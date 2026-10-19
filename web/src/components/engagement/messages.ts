/** SPEC-0027 — textos de seguir (§2 e §6). */
export const followMessages = {
  follow: "Seguir",
  following: "Seguindo",
  unfollow: "Deixar de seguir",
  empty:
    "Você ainda não segue ninguém. Siga sua cidade na agenda e as bandas e casas nos perfis.",
  unavailable:
    "Não foi possível carregar o que você segue agora. Tente de novo em instantes.",
  failed: "Não foi possível mudar agora. Tente de novo em instantes.",
} as const;

/** Nome acessível do botão: "Seguir {nome}" ou "Deixar de seguir {nome}". */
export function followLabel(name: string, following: boolean): string {
  return `${following ? followMessages.unfollow : followMessages.follow} ${name}`;
}

/** O anúncio da mudança, com o nome do alvo. */
export function followAnnouncement(name: string, following: boolean): string {
  return following
    ? `Você agora segue ${name}.`
    : `Você deixou de seguir ${name}.`;
}

const numberFormat = new Intl.NumberFormat("pt-BR");

/** Contagem por extenso (§6): "0 seguidores", "1 seguidor", "1.400 seguidores". */
export function followerCount(count: number): string {
  return `${numberFormat.format(count)} ${count === 1 ? "seguidor" : "seguidores"}`;
}

/** `/entrar` com a volta para `returnTo`. */
export function signInHref(returnTo: string): string {
  return `/entrar?returnTo=${encodeURIComponent(returnTo)}`;
}
