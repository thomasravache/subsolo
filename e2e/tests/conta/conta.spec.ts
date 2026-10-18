import { readFile } from "node:fs/promises";

import type { Download, Locator, Page } from "@playwright/test";

import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { accountTab, signIn, uniqueEmail } from "../auth/journey";

/**
 * SPEC-0019 — "Minha conta" (`/conta`, telas "Minha Conta - Subsolo" e "Excluir Conta - Subsolo" do Stitch) na aplicação
 * completa (pilha do Compose com o Mailpit; a entrada é pelo link mágico da SPEC-0016, lido na API do Mailpit).
 *
 * - E2E-01 (mobile-chrome e mobile-safari): o fã entra pelo link mágico e volta para `/conta`, troca o nome e vê
 *   "Nome salvo."; "Baixar meus dados" baixa `subsolo-meus-dados.json` com o e-mail e o nome; "Excluir minha conta",
 *   digita o e-mail e confirma em "Excluir para sempre"; cai em `/` com "Sua conta foi excluída.", e "Conta" leva a
 *   `/entrar`; entrando de novo com o mesmo e-mail, `/conta` mostra o nome vazio. Sem violações do axe em `/conta` e no
 *   diálogo.
 * - E2E-02 (mobile-chrome): nome com 61 caracteres mostra "Use de 1 a 60 caracteres." ligado ao campo; e-mail errado
 *   na confirmação mostra "O e-mail não confere." ligado ao campo e a conta continua; o foco fica preso no diálogo e
 *   "Cancelar" devolve o foco ao botão "Excluir minha conta".
 *
 * Cada teste usa um e-mail próprio (`@subsolo.test`). A entrada sempre leva `returnTo` e espera a página de destino
 * antes de navegar para outro lugar. Antes de a página hidratar (mobile-safari), um toque ou um texto digitado podem se
 * perder: as ações repetíveis são refeitas até o efeito esperado aparecer (`untilInteractive`).
 */
// SPEC-0010: o Caddy do Compose serve HTTPS com o certificado local (`tls internal`), não confiável.
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const accountPath = "/conta";
const exportFileName = "subsolo-meus-dados.json";

/** Prazo curto de cada tentativa enquanto a página ainda pode estar hidratando. */
const attempt = { timeout: 2_000 };

/**
 * Repete `action` até `outcome` passar. Só para ações que podem ser repetidas sem efeito diferente (preencher, salvar o
 * mesmo nome, abrir o diálogo, baixar, mandar a mesma confirmação errada).
 */
async function untilInteractive(
  action: () => Promise<void>,
  outcome: () => Promise<void>,
): Promise<void> {
  await expect(async () => {
    await action();
    await outcome();
  }).toPass({ intervals: [250, 500, 1_000, 2_000], timeout: 30_000 });
}

/** Entra pelo link mágico com `returnTo=/conta` e espera "Minha conta" com o e-mail. */
async function signInToAccount(page: Page, email: string): Promise<void> {
  await signIn(page, email, accountPath);
  await expect(page).toHaveURL((url) => url.pathname === accountPath);
  await expect(
    page.getByRole("heading", { level: 1, name: /minha conta/i }),
  ).toBeVisible();
  await expect(page.getByText(email)).toBeVisible();
}

/** Preenche o campo e confere o valor, preenchendo de novo se a hidratação o descartou. */
async function fillWhenInteractive(
  field: Locator,
  value: string,
): Promise<void> {
  await field.page().waitForLoadState("load");
  await untilInteractive(
    () => field.fill(value),
    () => expect(field).toHaveValue(value, attempt),
  );
}

/** O campo do nome de exibição em "Seus dados". */
function displayNameField(page: Page): Locator {
  return page.getByLabel("Nome de exibição");
}

/** Mensagens anunciadas (`aria-live`/`role=status`). */
function announcements(page: Page): Locator {
  return page.locator(
    '[aria-live="polite"], [aria-live="assertive"], [role="status"]',
  );
}

/** O diálogo de confirmação "Excluir conta?". */
function deleteDialog(page: Page): Locator {
  return page.getByRole("dialog", { name: "Excluir conta?" });
}

/** Abre a confirmação da exclusão e espera o diálogo com o aviso. */
async function openDeleteDialog(page: Page): Promise<Locator> {
  const dialog = deleteDialog(page);
  await untilInteractive(
    async () => {
      if (!(await dialog.isVisible())) {
        await page.getByRole("button", { name: "Excluir minha conta" }).click();
      }
    },
    () => expect(dialog).toBeVisible(attempt),
  );
  await expect(
    dialog.getByText(
      "Isso apaga sua conta e tudo o que o Subsolo guarda de você. Não dá para desfazer.",
    ),
  ).toBeVisible();
  return dialog;
}

/** O elemento com o foco está dentro de `container`? */
function hasFocusInside(container: Locator): Promise<boolean> {
  return container.evaluate((element) =>
    element.contains(element.ownerDocument.activeElement),
  );
}

test.describe("minha conta", () => {
  test("SPEC-0019:E2E-01 fã muda o nome, baixa os dados e exclui a conta [jornada: conta]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name === "desktop-chrome",
      "jornada do celular (mobile-chrome e mobile-safari)",
    );
    test.setTimeout(180_000);
    const email = uniqueEmail("conta01");

    // Entra e chega a /conta: "Seus dados", "Privacidade & dados", "Sessão atual" e "Excluir conta".
    await signInToAccount(page, email);
    for (const section of [
      /seus dados/i,
      /privacidade & dados/i,
      /sessão atual/i,
      /excluir conta/i,
    ]) {
      await expect(
        page.getByRole("heading", { level: 2, name: section }),
      ).toBeVisible();
    }
    await expect(
      page.getByRole("button", { name: "Sair da conta" }),
    ).toBeVisible();
    await axe.check(page);

    // Troca o nome: "Nome salvo." e o nome continua depois de recarregar.
    await fillWhenInteractive(displayNameField(page), "Caio Grind");
    await untilInteractive(
      () => page.getByRole("button", { name: "Salvar" }).click(),
      () =>
        expect(
          announcements(page).filter({ hasText: "Nome salvo." }),
        ).toBeVisible(attempt),
    );
    await page.reload();
    await expect(displayNameField(page)).toHaveValue("Caio Grind");

    // Baixa os dados: o arquivo tem o e-mail e o nome.
    let download: Download | undefined;
    await untilInteractive(
      async () => {
        const downloading = page.waitForEvent("download", attempt);
        await page.getByRole("button", { name: "Baixar meus dados" }).click();
        download = await downloading;
      },
      async () => expect(download).toBeDefined(),
    );
    if (!download) {
      throw new Error("o arquivo dos dados não foi baixado");
    }
    expect(download.suggestedFilename()).toBe(exportFileName);
    const file = JSON.parse(await readFile(await download.path(), "utf8")) as {
      account?: { email?: string; displayName?: string | null };
    };
    expect(file.account?.email).toBe(email);
    expect(file.account?.displayName).toBe("Caio Grind");

    // Exclui a conta digitando o e-mail.
    const dialog = await openDeleteDialog(page);
    await axe.check(page);
    await fillWhenInteractive(
      dialog.getByLabel("Digite seu e-mail para confirmar"),
      email,
    );
    await dialog.getByRole("button", { name: "Excluir para sempre" }).click();

    // Vai para / com o aviso; "Conta" passa a levar a /entrar.
    await expect(page).toHaveURL((url) => url.pathname === "/");
    await expect(page.getByText("Sua conta foi excluída.")).toBeVisible();
    const agenda = await page.goto("/cidade-exemplo");
    expect(agenda?.status()).toBe(200);
    await accountTab(page).click();
    await expect(page).toHaveURL((url) => url.pathname === "/entrar");

    // Entrando de novo com o mesmo e-mail, a conta é nova: nome vazio.
    await signInToAccount(page, email);
    await expect(displayNameField(page)).toHaveValue("");
  });

  test("SPEC-0019:E2E-02 nome longo e e-mail errado mostram a mensagem no campo; o diálogo prende o foco [jornada: conta]", async ({
    page,
    axe,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "mobile-chrome",
      "erros da conta: mobile-chrome",
    );
    test.setTimeout(180_000);
    const email = uniqueEmail("conta02");
    await signInToAccount(page, email);

    // Nome com 61 caracteres: a mensagem fica ligada ao campo.
    const name = displayNameField(page);
    await fillWhenInteractive(name, "a".repeat(61));
    await untilInteractive(
      () => page.getByRole("button", { name: "Salvar" }).click(),
      () =>
        expect(page.getByText("Use de 1 a 60 caracteres.")).toBeVisible(
          attempt,
        ),
    );
    await expect(name).toHaveAttribute("aria-invalid", "true");
    await expect(name).toHaveAccessibleDescription(
      /Use de 1 a 60 caracteres\./,
    );
    await axe.check(page);

    // E-mail errado na confirmação: a mensagem fica ligada ao campo e o diálogo continua aberto.
    const opener = page.getByRole("button", { name: "Excluir minha conta" });
    const dialog = await openDeleteDialog(page);
    expect(await hasFocusInside(dialog), "o foco não foi para o diálogo").toBe(
      true,
    );
    const confirm = dialog.getByLabel("Digite seu e-mail para confirmar");
    await fillWhenInteractive(confirm, uniqueEmail("outra-pessoa"));
    await untilInteractive(
      () => dialog.getByRole("button", { name: "Excluir para sempre" }).click(),
      () =>
        expect(dialog.getByText("O e-mail não confere.")).toBeVisible(attempt),
    );
    await expect(confirm).toHaveAttribute("aria-invalid", "true");
    await expect(confirm).toHaveAccessibleDescription(/O e-mail não confere\./);
    await expect(dialog).toBeVisible();
    await axe.check(page);

    // O foco fica preso no diálogo, para frente e para trás.
    for (const key of [
      ...Array<string>(6).fill("Tab"),
      ...Array<string>(6).fill("Shift+Tab"),
    ]) {
      await page.keyboard.press(key);
      expect(
        await hasFocusInside(dialog),
        `o foco saiu do diálogo depois de ${key}`,
      ).toBe(true);
    }

    // "Cancelar" fecha o diálogo e devolve o foco ao botão que o abriu.
    await dialog.getByRole("button", { name: "Cancelar" }).click();
    await expect(dialog).toBeHidden();
    await expect(opener).toBeFocused();

    // A conta continua.
    const again = await page.goto(accountPath);
    expect(again?.status()).toBe(200);
    await expect(page).toHaveURL((url) => url.pathname === accountPath);
    await expect(page.getByText(email)).toBeVisible();
  });
});
