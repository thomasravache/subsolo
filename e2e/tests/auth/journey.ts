import { execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";

import { expect, type Locator, type Page } from "@playwright/test";

import { composeProject, mailpitURL } from "./environment";

/**
 * SPEC-0016 — passos comuns das jornadas de entrada por link mágico: e-mail próprio de cada teste, leitura do link na
 * API HTTP do Mailpit (`MAILPIT_URL`), entrada completa pela interface e o Mailpit da pilha local parado e religado.
 */

/** Um e-mail novo e só deste teste (`@subsolo.test`). */
export function uniqueEmail(prefix: string): string {
  const random = randomUUID().replaceAll("-", "").slice(0, 12);
  return `${prefix}-${Date.now().toString(36)}-${random}@subsolo.test`;
}

interface MailpitSummary {
  ID: string;
  To: { Address: string }[];
}

/** Os links mágicos (`/entrar/confirmar?token=…`) dos e-mails mandados para `email`, do mais novo ao mais antigo. */
async function magicLinks(email: string): Promise<string[]> {
  const query = encodeURIComponent(`to:"${email}"`);
  const search = await fetch(
    `${mailpitURL}/api/v1/search?query=${query}&limit=50`,
  );
  if (!search.ok) {
    return [];
  }
  const { messages } = (await search.json()) as {
    messages: MailpitSummary[];
  };
  const links: string[] = [];
  for (const message of messages.filter((m) =>
    m.To.some((to) => to.Address.toLowerCase() === email.toLowerCase()),
  )) {
    const detail = await fetch(
      `${mailpitURL}/api/v1/message/${encodeURIComponent(message.ID)}`,
    );
    const { Text } = (await detail.json()) as { Text?: string };
    const link =
      /https?:\/\/[^\s"<>]+\/entrar\/confirmar\?token=[^\s"<>]+/.exec(
        Text ?? "",
      )?.[0];
    if (link) {
      links.push(link);
    }
  }
  return links;
}

/**
 * Espera o `count`-ésimo e-mail para `email` chegar ao Mailpit e devolve o caminho do link mais novo
 * (`/entrar/confirmar?token=…`), para abrir na mesma origem dos testes.
 */
export async function readMagicLink(email: string, count = 1): Promise<string> {
  let links: string[] = [];
  await expect
    .poll(
      async () => {
        links = await magicLinks(email);
        return links.length;
      },
      {
        message: `o e-mail do link para ${email} não chegou ao Mailpit (${mailpitURL})`,
        timeout: 15_000,
      },
    )
    .toBeGreaterThanOrEqual(count);
  const link = new URL(links[0] ?? "");
  expect(link.pathname).toBe("/entrar/confirmar");
  expect(link.searchParams.get("token")).toBeTruthy();
  return `${link.pathname}${link.search}`;
}

/** Quantos e-mails o Mailpit recebeu para `email`. */
export async function messageCount(email: string): Promise<number> {
  return (await magicLinks(email)).length;
}

/**
 * SPEC-0019 (Emenda 1) — espera o React hidratar o elemento (a página SSR fica visível antes dos handlers): o React
 * marca cada nó hidratado com as propriedades internas `__reactFiber$…`/`__reactProps$…`. Antes disso, o texto
 * digitado se perde e um envio do formulário vira um GET nativo para `/entrar?email=…` (sem pedir o link e sem o
 * `returnTo`). Não depende de código de produção nem de espera fixa.
 */
export async function waitForHydration(
  element: Locator,
  timeout = 15_000,
): Promise<void> {
  await expect
    .poll(
      () =>
        element.evaluate((node) =>
          Object.keys(node).some(
            (key) =>
              key.startsWith("__reactProps$") ||
              key.startsWith("__reactFiber$"),
          ),
        ),
      {
        message:
          "a página não ficou interativa (React não hidratou o elemento)",
        timeout,
      },
    )
    .toBe(true);
}

/** Preenche o campo já hidratado e confere que ele guardou o valor. */
export async function fillInteractive(
  field: Locator,
  value: string,
): Promise<void> {
  await waitForHydration(field);
  await field.fill(value);
  await expect(field).toHaveValue(value);
}

/** Pede o link em `/entrar` (opcionalmente com `returnTo`) e espera "Confira seu e-mail". */
export async function requestLink(
  page: Page,
  email: string,
  returnTo?: string,
): Promise<void> {
  const path = returnTo
    ? `/entrar?returnTo=${encodeURIComponent(returnTo)}`
    : "/entrar";
  const response = await page.goto(path);
  expect(response?.status(), `GET ${path}`).toBe(200);
  const submit = page.getByRole("button", { name: "Mandar link de acesso" });
  await waitForHydration(submit);
  await fillInteractive(page.getByLabel("Seu e-mail"), email);
  await submit.click();
  await expect(
    page.getByRole("heading", { name: /confira seu e-mail/i }),
  ).toBeVisible();
}

/** Abre o link mágico e toca em "Entrar no Subsolo". */
export async function confirmLink(page: Page, linkPath: string): Promise<void> {
  const response = await page.goto(linkPath);
  expect(response?.status(), `GET ${linkPath}`).toBe(200);
  await expect(
    page.getByRole("heading", { level: 1, name: /quase lá/i }),
  ).toBeVisible();
  const enter = page.getByRole("button", { name: "Entrar no Subsolo" });
  await waitForHydration(enter);
  await enter.click();
}

/**
 * Entrada completa pela interface: pede o link, lê no Mailpit, abre e confirma; devolve o caminho do link usado.
 * SPEC-0019 (Emenda 1) — só volta depois da navegação de pós-entrada (para `returnTo`, ou `/` sem ele), para nenhum
 * chamador navegar enquanto a entrada ainda grava o cookie.
 */
export async function signIn(
  page: Page,
  email: string,
  returnTo?: string,
): Promise<string> {
  const before = await messageCount(email);
  await requestLink(page, email, returnTo);
  const linkPath = await readMagicLink(email, before + 1);
  await confirmLink(page, linkPath);
  const destination = returnTo ?? "/";
  await expect(page).toHaveURL(
    (url) => url.pathname === new URL(destination, url).pathname,
    { timeout: 15_000 },
  );
  await page.waitForLoadState("load");
  return linkPath;
}

/** A aba "Conta" da navegação inferior (celular). */
export function accountTab(page: Page) {
  return page
    .getByRole("navigation", { name: "Navegação inferior" })
    .getByRole("link", { name: /conta/i });
}

function mailpitContainer(): string {
  const id = execFileSync(
    "docker",
    [
      "ps",
      "--all",
      "--quiet",
      "--filter",
      `label=com.docker.compose.project=${composeProject}`,
      "--filter",
      "label=com.docker.compose.service=mailpit",
    ],
    { encoding: "utf8" },
  ).trim();
  expect(id, `contêiner do Mailpit do projeto ${composeProject}`).toBeTruthy();
  return id.split("\n")[0] ?? "";
}

/** Espera `probe` responder `true` duas vezes seguidas (falha de rede conta como `false`), até `timeout` ms. */
async function waitUntilStable(
  probe: () => Promise<boolean>,
  message: string,
  timeout = 60_000,
): Promise<void> {
  let streak = 0;
  await expect
    .poll(
      async () => {
        let ok = false;
        try {
          ok = await probe();
        } catch {
          ok = false;
        }
        streak = ok ? streak + 1 : 0;
        return streak;
      },
      { message, timeout, intervals: [250, 500, 1_000] },
    )
    .toBeGreaterThanOrEqual(2);
}

/**
 * Deixa o Mailpit da pilha local (só o deste projeto do Compose) mudo enquanto `action` roda: o contêiner fica
 * congelado (`docker pause`), então a API conecta no SMTP e não recebe resposta, e o envio cai no timeout de 10 s
 * (`notifications.email_unavailable`). Congelar, em vez de parar e religar, não recria a interface de rede do
 * contêiner (o Chromium acusaria `ERR_NETWORK_CHANGED` nos testes seguintes) nem muda o IP do Mailpit. No fim, o
 * contêiner volta e o teste só termina quando a API HTTP do Mailpit (`MAILPIT_URL`) e o site (`/health` pelo navegador)
 * respondem de novo, com prazo.
 */
export async function withMailpitSilent(
  page: Page,
  action: () => Promise<void>,
): Promise<void> {
  const container = mailpitContainer();
  execFileSync("docker", ["pause", container]);
  try {
    await action();
  } finally {
    execFileSync("docker", ["unpause", container]);
    await waitUntilStable(
      async () => (await fetch(`${mailpitURL}/api/v1/info`)).ok,
      `a API do Mailpit (${mailpitURL}) não voltou depois do teste`,
    );
    await waitUntilStable(
      async () => (await page.goto("/health"))?.ok() ?? false,
      "o site não voltou a responder pelo navegador depois do teste",
    );
  }
}
