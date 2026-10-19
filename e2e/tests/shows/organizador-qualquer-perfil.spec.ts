import { expect, test } from "../fixtures";
import { appURL } from "../agenda/environment";
import { fillInteractive, signIn } from "../auth/journey";
import {
  adminEmail,
  announcements,
  approvedProducer,
  chooseCity,
  flyerJpeg,
  localDate,
  mutate,
  newShowPath,
  pickStyle,
  press,
  uniqueSuffix,
} from "./journey";

/**
 * SPEC-0026 H23-08 (revisão da SPEC-0023) — no cadastro de show, a busca de perfis acha qualquer perfil aprovado do
 * tipo, com ou sem dono:
 * - o admin escolhe como organizador o perfil de produtor de outro dono (antes só achava os sem dono);
 * - a sugestão de casa oferece a casa aprovada que tem dono (antes só as sem dono), e o show fica ligado ao perfil dela
 *   (`venue.profileSlug` no detalhe; o id entra em `ShowPublished.ProfileIds`).
 * Preparação pela API: o produtor aprovado (SPEC-0023) pede também o perfil de casa, que o admin aprova. Projeto
 * mobile-chrome.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

test("SPEC-0026:H23-08 o admin escolhe o organizador e a casa de outro dono no cadastro de show", async ({
  page,
  browser,
}, testInfo) => {
  test.skip(
    testInfo.project.name !== "mobile-chrome",
    "verificação em um projeto (mobile-chrome)",
  );
  test.setTimeout(300_000);

  // O dono: produtor aprovado e uma casa aprovada, os dois dele.
  const ownerContext = await browser.newContext({
    baseURL: appURL,
    ignoreHTTPSErrors: true,
  });
  const suffix = uniqueSuffix();
  const venueName = `Porao Alheio ${suffix}`;
  let producer: Awaited<ReturnType<typeof approvedProducer>>;
  try {
    const ownerPage = await ownerContext.newPage();
    producer = await approvedProducer(ownerPage, browser, "h2308");
    const request = (await mutate(
      ownerPage.request,
      "post",
      "/api/v1/profiles/requests",
      {
        type: "venue",
        name: venueName,
        city: producer.city.slug,
        styles: [],
        links: {},
        contact: "@porao.alheio no Instagram",
      },
    )) as { requestId: string };
    await signIn(page, adminEmail, "/conta");
    await mutate(
      page.request,
      "post",
      `/api/v1/admin/profile-requests/${request.requestId}/approve`,
      null,
    );
  } finally {
    await ownerContext.close();
  }

  // O admin cadastra o show.
  await page.goto(newShowPath);
  const form = page.locator("main form");
  const title = `Noite Alheia ${suffix.slice(-5)}`;

  const search = form.getByLabel(/buscar perfil/i);
  await fillInteractive(search, producer.profileName);
  await press(
    form.getByRole("button", { name: `Usar ${producer.profileName}` }),
  );
  const organizer = form.getByLabel("Perfil organizador");
  await expect(organizer).toHaveValue(producer.profileId);

  const uploaded = page.waitForResponse(
    (response) =>
      response.url().endsWith("/api/v1/flyers") &&
      response.request().method() === "POST",
  );
  await form.getByLabel(/flyer/i).setInputFiles({
    name: "flyer.jpg",
    mimeType: "image/jpeg",
    buffer: await flyerJpeg(page, title),
  });
  expect([200, 201]).toContain((await uploaded).status());

  await fillInteractive(form.getByLabel(/^nome( do (evento|show))?$/i), title);
  await fillInteractive(form.getByLabel(/^data$/i), localDate(25));
  await fillInteractive(form.getByLabel(/^(hora|horário)$/i), "22:00");
  await fillInteractive(form.getByLabel(/casa/i).first(), venueName);
  await press(form.getByRole("button", { name: `Usar o perfil ${venueName}` }));
  await chooseCity(form, producer.city);
  const bands = form.getByRole("textbox", { name: /banda/i });
  if ((await bands.count()) === 0) {
    await press(
      form.getByRole("button", { name: /adicionar (outra )?banda/i }),
    );
  }
  await fillInteractive(bands.nth(0), "Cova Rasa");
  await pickStyle(form, /^death( metal)?$/i);
  await fillInteractive(form.getByLabel(/pre[çc]o/i), "R$ 30");
  await press(form.getByRole("button", { name: /publicar show/i }));
  await expect(
    announcements(page).filter({ hasText: "Show publicado." }),
  ).toBeVisible();

  // O show ficou com o organizador e a casa do outro dono.
  const agenda = await page.request.get(
    `/api/v1/cities/${producer.city.slug}/agenda`,
  );
  const { shows } = (await agenda.json()) as {
    shows: { id: string; slug: string; title: string }[];
  };
  const created = shows.find((item) => item.title === title);
  expect(created, `show ${title} fora da agenda`).toBeDefined();
  const edit = await page.request.get(`/api/v1/shows/${created?.id}`);
  expect(edit.status()).toBe(200);
  expect(
    ((await edit.json()) as { organizerProfileId: string }).organizerProfileId,
  ).toBe(producer.profileId);
  const detail = await page.request.get(
    `/api/v1/cities/${producer.city.slug}/shows/${created?.slug}`,
  );
  expect(detail.status()).toBe(200);
  const body = (await detail.json()) as {
    venue: { name: string; profileSlug: string | null };
  };
  expect(body.venue.name).toBe(venueName);
  expect(body.venue.profileSlug, "a casa não ficou ligada ao perfil").toBe(
    venueName.toLowerCase().replace(/[^a-z0-9]+/g, "-"),
  );
});
