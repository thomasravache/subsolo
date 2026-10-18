import { execFileSync } from "node:child_process";

import { expect, test } from "../fixtures";
import { appURL, externalApp } from "../agenda/environment";
import { composeProject } from "../auth/environment";
import { uniqueSuffix } from "./journey";

/**
 * SPEC-0023 H22-02 (revisão da SPEC-0022) — o Caddy do Compose em `/media/*`:
 * - o `Cache-Control` imutável vale só para respostas 200: um flyer inexistente dá 404 sem `immutable` (um 404 não pode
 *   ficar um ano no cache do navegador nem de intermediários);
 * - o `file_server` esconde arquivos com ponto no nome (`hide .*`): o temporário de uma gravação em andamento
 *   (`.<nome>.<guid>.tmp`, do `DiskFileStorage`) nunca é servido. O temporário é posto no volume `media` do projeto do
 *   Compose local por um contêiner de passagem (só na pilha local; contra um `BASE_URL` externo, só a primeira parte).
 * SPEC-0024 H23-04 (revisão da SPEC-0023) — um diretório (`/media/flyers/`, `/media/`) também casa o matcher `file` do
 * Caddy: ele nunca pode sair com o cache imutável.
 */
test.use({ baseURL: appURL, ignoreHTTPSErrors: true });

const missing = `/media/flyers/${"0".repeat(64)}-thumb.webp`;

/** O volume `media` do projeto do Compose local. */
function mediaVolume(): string {
  const volume = execFileSync(
    "docker",
    [
      "volume",
      "ls",
      "--quiet",
      "--filter",
      `label=com.docker.compose.project=${composeProject}`,
      "--filter",
      "label=com.docker.compose.volume=media",
    ],
    { encoding: "utf8" },
  ).trim();
  expect(volume, `volume media do projeto ${composeProject}`).toBeTruthy();
  return volume.split("\n")[0] ?? "";
}

test.describe("mídia pelo Caddy", () => {
  test("SPEC-0023:H22-02 flyer inexistente dá 404 sem o cache imutável", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    const response = await request.get(missing);

    expect(response.status()).toBe(404);
    expect(
      response.headers()["cache-control"] ?? "",
      "o 404 de /media saiu com o Cache-Control imutável",
    ).not.toContain("immutable");
  });

  test("SPEC-0024:H23-04 um diretório de /media nunca sai com o cache imutável", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );

    for (const directory of ["/media/flyers/", "/media/"]) {
      const response = await request.get(directory);
      expect(
        response.headers()["cache-control"] ?? "",
        `GET ${directory} (${response.status()}) saiu com o Cache-Control imutável`,
      ).not.toContain("immutable");
    }
  });

  test("SPEC-0023:H22-02 arquivo com ponto no nome (temporário) não é servido", async ({
    request,
  }, testInfo) => {
    test.skip(
      testInfo.project.name !== "desktop-chrome",
      "verificação do servidor: basta um projeto",
    );
    test.skip(externalApp, "precisa do volume do Compose local");
    const name = `.probe-${uniqueSuffix()}.tmp`;
    execFileSync("docker", [
      "run",
      "--rm",
      "--volume",
      `${mediaVolume()}:/srv/media`,
      "caddy:2.11.7-alpine",
      "sh",
      "-c",
      `mkdir -p /srv/media/flyers && printf temporario > /srv/media/flyers/${name}`,
    ]);

    const response = await request.get(`/media/flyers/${name}`);

    expect(response.status(), `GET /media/flyers/${name}`).toBe(404);
  });
});
