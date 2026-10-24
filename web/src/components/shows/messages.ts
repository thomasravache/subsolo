import type { ApiResult } from "@/api/browser";

import { cityTimeParts } from "./city-time";

/** SPEC-0023 — textos das telas de shows (§6 e telas "Cadastrar Show" e "Meus Shows - Produtor" do Stitch). */
export const showMessages = {
  published: "Show publicado.",
  saved: "Alterações salvas.",
  postponed: "Show adiado.",
  cancelled: "Show cancelado.",
  empty: "Nenhum show cadastrado ainda.",
  emptyHint: "Toque em + Novo show para cadastrar o primeiro",
  emptyPast: "Nenhum show passado.",
  cancelQuestion:
    "Cancelar este show? Ele continua na agenda com o selo CANCELADO.",
  flyerMissing: "Envie o flyer do show.",
  flyerUploading: "Enviando o flyer…",
  flyerSent: "Flyer enviado.",
  flyerInvalidType: "O flyer precisa ser JPEG, PNG ou WebP.",
  flyerTooLarge: "O flyer passa de 10 MB.",
  flyerTooManyPixels: "O flyer passa de 40 megapixels.",
  flyerUnprocessable: "Não deu para ler esse arquivo. Tente outra imagem.",
  flyerBusy: "O envio está demorando. Tente de novo em instantes.",
  flyerNotFound: "O flyer expirou. Envie de novo.",
  slugTaken: "Já existe um show com esse nome nessa data nessa cidade.",
  notFound: "Esse show não existe mais.",
  /** SPEC-0033 — selo e erro do show oculto pela moderação. */
  hiddenBadge: "Oculto pela moderação",
  hidden: "Este show foi ocultado pela moderação e não pode mais ser alterado.",
  noProfile:
    "Para cadastrar shows você precisa de um perfil aprovado de banda, casa ou produtor.",
  rateLimited: "Muitos pedidos seguidos. Espere um pouco e tente de novo.",
  generic: "Algo deu errado. Tente de novo em instantes.",
  unavailable: "Os shows estão fora do ar por alguns instantes. Tente de novo.",
} as const;

/** Para onde o cadastro leva: "Meus shows" com "Show publicado.". */
export const publishedPath = "/conta/shows?aviso=show-publicado";

/** Para onde a edição leva: "Meus shows" com "Alterações salvas.". */
export const savedPath = "/conta/shows?aviso=alteracoes-salvas";

/** Os avisos de `?aviso=` em "Meus shows". */
export const showNotices: Record<string, string> = {
  "show-publicado": showMessages.published,
  "alteracoes-salvas": showMessages.saved,
};

export const myShowsPath = "/conta/shows";

export const newShowPath = "/conta/shows/novo";

/** A tela de edição do show `id`. */
export function editShowPath(id: string): string {
  return `/conta/shows/${id}`;
}

/** Mensagem de um erro da API sem campo próprio. */
export function showErrorMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
): string {
  switch (result.code) {
    case "events.show_slug_taken":
      return showMessages.slugTaken;
    case "events.show_not_found":
      return showMessages.notFound;
    case "events.flyer_not_found":
      return showMessages.flyerNotFound;
    case "events.show_hidden":
      return showMessages.hidden;
    default:
      if (result.status === 429) {
        return showMessages.rateLimited;
      }
      if (result.status === 401) {
        return "Sua sessão expirou. Entre de novo para continuar.";
      }
      if (result.status === 403) {
        return "Você não tem acesso a este show.";
      }
      return showMessages.generic;
  }
}

/** Mensagem de uma falha no envio do flyer. */
export function flyerErrorMessage(
  result: Extract<ApiResult<unknown>, { ok: false }>,
): string {
  switch (result.code) {
    case "events.flyer_invalid_type":
      return showMessages.flyerInvalidType;
    case "events.flyer_too_large":
      return showMessages.flyerTooLarge;
    case "events.flyer_too_many_pixels":
      return showMessages.flyerTooManyPixels;
    case "events.flyer_unprocessable":
      return showMessages.flyerUnprocessable;
    case "service.unavailable":
      return showMessages.flyerBusy;
    default:
      return showErrorMessage(result);
  }
}

/** Dia, mês, dia da semana e hora ("22:00") de `iso` no fuso `timeZone` da cidade. */
export function showDateParts(iso: string, timeZone: string) {
  const { day, month, weekday, hour, minute } = cityTimeParts(iso, timeZone);
  return { day, month, weekday, time: `${hour}:${minute}` };
}
