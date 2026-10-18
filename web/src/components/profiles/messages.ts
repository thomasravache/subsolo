import type { ApiResult } from "@/api/browser";
import type { CitySummary } from "@/api/client";

/** SPEC-0021 — textos das telas de perfis (§6 e telas "Meus perfis", "Pedir Perfil", "Reivindicar Perfil" e "Fila do Admin"). */
export const profileMessages = {
  requestSent: "Pedido enviado.",
  profileCreated: "Perfil criado.",
  approved: "Pedido aprovado.",
  rejected: "Pedido recusado.",
  approvalNotice: "Seu pedido passa por aprovação do admin da cena",
  empty:
    "Você ainda não tem perfil. Toca uma banda, tem uma casa ou produz shows? Peça o seu.",
  requestPending:
    "Você já tem um pedido em análise. Espere a decisão do admin para pedir outro.",
  nameTaken: "Já existe um perfil desse tipo com esse nome.",
  notFound: "Esse perfil não existe mais. Busque de novo.",
  alreadyOwned: "Esse perfil já tem dono.",
  requestNotFound: "Esse pedido não existe mais. Recarregue a página.",
  requestDecided: "Esse pedido já foi decidido. Recarregue a página.",
  rateLimited: "Muitos pedidos seguidos. Espere um pouco e tente de novo.",
  generic: "Algo deu errado. Tente de novo em instantes.",
  unavailable:
    "Os perfis estão fora do ar por alguns instantes. Tente de novo.",
  searchUnavailable: "A busca está fora do ar. Tente de novo em instantes.",
  noResults: "Nenhum perfil sem dono com esse nome.",
} as const;

/** Para onde o pedido (ou a reivindicação) enviado leva: "Meus perfis" com o aviso "Pedido enviado.". */
export const requestSentPath = "/conta/perfis?aviso=pedido-enviado";

/** Valor de `?aviso=` que mostra "Pedido enviado." em "Meus perfis". */
export const requestSentNotice = "pedido-enviado";

/** Mensagem de um erro da API sem campo próprio. */
export function profileErrorMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
): string {
  switch (result.code) {
    case "profiles.request_pending":
      return profileMessages.requestPending;
    case "profiles.name_taken":
      return profileMessages.nameTaken;
    case "profiles.not_found":
      return profileMessages.notFound;
    case "profiles.already_owned":
      return profileMessages.alreadyOwned;
    case "profiles.request_not_found":
      return profileMessages.requestNotFound;
    case "profiles.request_decided":
      return profileMessages.requestDecided;
    default:
      if (result.status === 429) {
        return profileMessages.rateLimited;
      }
      if (result.status === 401) {
        return "Sua sessão expirou. Entre de novo para continuar.";
      }
      if (result.status === 403) {
        return "Você não tem acesso a esta ação.";
      }
      return profileMessages.generic;
  }
}

/** Nome de exibição de cada tipo de perfil. */
export const profileTypeLabels = {
  band: "Banda",
  venue: "Casa / Local",
  producer: "Produtor",
} as const;

/** O nome do tipo (ou o próprio código, se desconhecido). */
export function profileTypeLabel(type: string): string {
  return profileTypeLabels[type as keyof typeof profileTypeLabels] ?? type;
}

/** O nome da cidade pelo slug (ou o próprio slug, se a cidade não estiver na lista). */
export function cityName(cities: CitySummary[], slug: string): string {
  return cities.find((city) => city.slug === slug)?.name ?? slug;
}
