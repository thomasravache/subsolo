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

/**
 * Nome acessível do botão de seguir, constante (SPEC-0029, H27-04; WCAG 2.5.3): "Seguir {nome}" — o estado vai no
 * `aria-pressed`, não no nome.
 */
export function followLabel(name: string): string {
  return `${followMessages.follow} ${name}`;
}

/** Nome acessível do "Deixar de seguir" da lista "Seguindo": "Deixar de seguir {nome}". */
export function unfollowLabel(name: string): string {
  return `${followMessages.unfollow} ${name}`;
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

/** SPEC-0029 — textos do "vou" (§6). */
export const attendMessages = {
  attend: "Vou",
  attending: "Vou ✓",
  failed: "Não foi possível marcar agora. Tente de novo em instantes.",
} as const;

/** Nome acessível do botão "Vou", constante (H27-04; WCAG 2.5.3): "Vou ao show {título}" — o estado vai no `aria-pressed`. */
export function attendLabel(title: string): string {
  return `Vou ao show ${title}`;
}

/** Quantos vão (§6): "0 vão", "1 vai", "1.400 vão". */
export function goingCount(count: number): string {
  return `${numberFormat.format(count)} ${count === 1 ? "vai" : "vão"}`;
}

/** Quantos foram, no show que já passou (§6): "1 foi", "12 foram". */
export function wentCount(count: number): string {
  return `${numberFormat.format(count)} ${count === 1 ? "foi" : "foram"}`;
}

/** O anúncio da mudança, com o título do show e a nova contagem (se houver). */
export function attendAnnouncement(
  title: string,
  attending: boolean,
  count: number | undefined,
): string {
  const change = attending
    ? `Você marcou que vai a ${title}.`
    : `Você desmarcou o vou em ${title}.`;
  return count === undefined ? change : `${change} ${goingCount(count)}.`;
}
