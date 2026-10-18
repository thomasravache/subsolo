import { randomUUID } from "node:crypto";

import type {
  APIRequestContext,
  Browser,
  Locator,
  Page,
} from "@playwright/test";

import { expect } from "../fixtures";
import { appURL } from "../agenda/environment";
import { signIn, uniqueEmail, waitForHydration } from "../auth/journey";

/**
 * SPEC-0023 — passos das jornadas do cadastro de show na pilha do Compose: o produtor aprovado é preparado pela API
 * (cidade nova criada pelo admin, pedido do perfil de produtor e aprovação; o papel `artist` chega pela outbox), para a
 * jornada começar em "Meus shows". Cada execução usa nomes, cidade e e-mails próprios: a pilha pode ser reaproveitada,
 * e os shows ficam numa cidade só deles (a agenda vazia da cidade de exemplo, SPEC-0008, continua vazia).
 */
export const adminEmail = "admin@subsolo.test";
export const myShowsPath = "/conta/shows";
export const newShowPath = "/conta/shows/novo";

/** Sufixo novo por execução. */
export function uniqueSuffix(): string {
  return `${Date.now().toString(36)}${randomUUID().replaceAll("-", "").slice(0, 4)}`;
}

/** Toca no controle depois de a página hidratá-lo (antes disso o toque se perde). */
export async function press(control: Locator): Promise<void> {
  await waitForHydration(control);
  await control.click();
}

/** Mensagens anunciadas (`aria-live`/`role=status`). */
export function announcements(page: Page): Locator {
  return page.locator(
    '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
  );
}

/** O cartão (`article`) com o texto. */
export function card(scope: Page | Locator, text: string): Locator {
  return scope.getByRole("article").filter({ hasText: text });
}

/** Mutação na API com o token anti-forgery novo (cookies do contexto do navegador). */
export async function mutate(
  request: APIRequestContext,
  method: "post" | "put",
  path: string,
  data: unknown,
): Promise<unknown> {
  const antiforgery = await request.get("/api/v1/antiforgery");
  expect(antiforgery.status(), "GET /api/v1/antiforgery").toBe(200);
  const { token } = (await antiforgery.json()) as { token: string };
  const response = await request[method](path, {
    data,
    headers: { "X-CSRF": token },
  });
  expect(
    response.ok(),
    `${method.toUpperCase()} ${path}: ${response.status()} ${await response.text()}`,
  ).toBe(true);
  return response.status() === 204 ? undefined : await response.json();
}

export interface Producer {
  email: string;
  profileName: string;
  profileId: string;
  city: { slug: string; name: string };
}

/**
 * Prepara pela API um produtor aprovado: o admin (noutro contexto) cria a cidade `Curitiba <sufixo>`; o usuário entra
 * (link mágico), pede o perfil de produtor nessa cidade; o admin aprova; espera o papel `artist` em `/api/v1/me`. A
 * página termina logada em `/conta`.
 */
export async function approvedProducer(
  page: Page,
  browser: Browser,
  prefix: string,
): Promise<Producer> {
  const suffix = uniqueSuffix();
  const email = uniqueEmail(prefix);
  const profileName = `Peste Produções ${suffix}`;
  const admin = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  try {
    const adminPage = await admin.newPage();
    await signIn(adminPage, adminEmail, "/conta");
    const cityName = `Curitiba ${suffix}`;
    const city = (await mutate(
      adminPage.request,
      "post",
      "/api/v1/admin/cities",
      {
        name: cityName,
        state: "PR",
        timeZone: "America/Sao_Paulo",
        slug: null,
      },
    )) as { slug: string };

    await signIn(page, email, "/conta");
    const request = (await mutate(
      page.request,
      "post",
      "/api/v1/profiles/requests",
      {
        type: "producer",
        name: profileName,
        city: city.slug,
        styles: [],
        links: {},
        contact: "@peste.producoes no Instagram",
      },
    )) as { requestId: string };
    const approved = (await mutate(
      adminPage.request,
      "post",
      `/api/v1/admin/profile-requests/${request.requestId}/approve`,
      null,
    )) as { profileId: string };

    await expect
      .poll(
        async () => {
          const me = await page.request.get("/api/v1/me");
          const body = (await me.json()) as { roles?: string[] };
          return body.roles ?? [];
        },
        {
          message: "o papel artist não chegou depois da aprovação do perfil",
          timeout: 30_000,
        },
      )
      .toContain("artist");

    return {
      email,
      profileName,
      profileId: approved.profileId,
      city: { slug: city.slug, name: cityName },
    };
  } finally {
    await admin.close();
  }
}

/**
 * Um flyer JPEG vertical (600×800) desenhado no navegador com o texto `label` (conteúdo único por execução: o id do
 * flyer é o hash do arquivo).
 */
export async function flyerJpeg(page: Page, label: string): Promise<Buffer> {
  const base64 = await page.evaluate(async (text) => {
    const canvas = document.createElement("canvas");
    canvas.width = 600;
    canvas.height = 800;
    const context = canvas.getContext("2d");
    if (!context) {
      throw new Error("canvas 2d indisponível");
    }
    context.fillStyle = "#111111";
    context.fillRect(0, 0, 600, 800);
    context.fillStyle = "#e5e5e5";
    context.font = "bold 48px sans-serif";
    context.fillText(text, 40, 400);
    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, "image/jpeg", 0.9),
    );
    if (!blob) {
      throw new Error("canvas.toBlob não gerou o JPEG");
    }
    const bytes = new Uint8Array(await blob.arrayBuffer());
    let binary = "";
    for (const byte of bytes) {
      binary += String.fromCharCode(byte);
    }
    return btoa(binary);
  }, label);
  return Buffer.from(base64, "base64");
}

/** A data local (São Paulo) daqui a `days` dias, no formato do campo `date` (`aaaa-mm-dd`). */
export function localDate(days: number): string {
  const date = new Date(Date.now() + days * 24 * 60 * 60 * 1000);
  return new Intl.DateTimeFormat("en-CA", {
    timeZone: "America/Sao_Paulo",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(date);
}

/** Escolhe a cidade: um `<select>` (pelo texto da opção) ou um campo de texto/autocompletar. */
export async function chooseCity(
  form: Locator,
  city: { slug: string; name: string },
): Promise<void> {
  const field = form.getByLabel("Cidade", { exact: true });
  await waitForHydration(field);
  const tag = await field.evaluate((element) => element.tagName.toLowerCase());
  if (tag === "select") {
    await field.selectOption({ label: city.name });
    await expect(field).toHaveValue(city.slug);
    return;
  }
  await field.fill(city.name);
  const option = form.page().getByRole("option", { name: city.name });
  if ((await option.count()) > 0) {
    await option.first().click();
  }
}

/** Marca o estilo (caixa de seleção ou chip com estado pressionado). */
export async function pickStyle(form: Locator, style: RegExp): Promise<void> {
  const control = form
    .getByRole("checkbox", { name: style })
    .or(form.getByRole("button", { name: style }))
    .first();
  await press(control);
}

/** `POST /api/v1/flyers` pela API (cookies do contexto); devolve o corpo do §6 da SPEC-0022. */
export async function uploadFlyer(
  page: Page,
  label: string,
): Promise<{ flyerId: string; thumbUrl: string }> {
  const antiforgery = await page.request.get("/api/v1/antiforgery");
  const { token } = (await antiforgery.json()) as { token: string };
  const response = await page.request.post("/api/v1/flyers", {
    headers: { "X-CSRF": token },
    multipart: {
      file: {
        name: "flyer.jpg",
        mimeType: "image/jpeg",
        buffer: await flyerJpeg(page, label),
      },
    },
  });
  expect(
    [200, 201],
    `POST /api/v1/flyers: ${response.status()} ${await response.text()}`,
  ).toContain(response.status());
  return (await response.json()) as { flyerId: string; thumbUrl: string };
}
