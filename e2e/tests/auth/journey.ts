import { execFileSync } from "node:child_process";

import { expect, type Page } from "@playwright/test";

import { composeProject, mailpitURL } from "./environment";

/**
 * SPEC-0016 — passos comuns das jornadas de entrada por link mágico: e-mail próprio de cada teste, leitura do link na
 * API HTTP do Mailpit (`MAILPIT_URL`), entrada completa pela interface e o Mailpit da pilha local parado e religado.
 */

/** Um e-mail novo e só deste teste (`@subsolo.test`). */
export function uniqueEmail(prefix: string): string {
  const random = Math.random().toString(36).slice(2, 10);
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
  await page.getByLabel("Seu e-mail").fill(email);
  await page.getByRole("button", { name: "Mandar link de acesso" }).click();
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
  await page.getByRole("button", { name: "Entrar no Subsolo" }).click();
}

/** Entrada completa pela interface: pede o link, lê no Mailpit, abre e confirma; devolve o caminho do link usado. */
export async function signIn(
  page: Page,
  email: string,
  returnTo?: string,
): Promise<string> {
  const before = await messageCount(email);
  await requestLink(page, email, returnTo);
  const linkPath = await readMagicLink(email, before + 1);
  await confirmLink(page, linkPath);
  if (returnTo) {
    await expect(page).toHaveURL(
      (url) => url.pathname === new URL(returnTo, url).pathname,
    );
  }
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

/** Para o Mailpit da pilha local (só o deste projeto do Compose) enquanto `action` roda, e o religa depois. */
export async function withMailpitStopped(
  action: () => Promise<void>,
): Promise<void> {
  const container = mailpitContainer();
  execFileSync("docker", ["stop", "--time", "1", container]);
  try {
    await action();
  } finally {
    execFileSync("docker", ["start", container]);
    await expect
      .poll(
        async () => {
          try {
            return (await fetch(`${mailpitURL}/api/v1/info`)).ok;
          } catch {
            return false;
          }
        },
        { message: "o Mailpit não voltou depois do teste", timeout: 30_000 },
      )
      .toBe(true);
  }
}
