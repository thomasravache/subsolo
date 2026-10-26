// @vitest-environment node
import { afterEach, describe, expect, it, vi } from "vitest";

import nextConfig from "../next.config";

/**
 * SPEC-0015 UT-01 — o Next.js 16 só entrega os metadados (o `<title>`) de forma bloqueante para os agentes que casam
 * com `htmlLimitedBots`; para os demais, o título sai em streaming, num bloco à parte, e o documento fica sem
 * `<title>` durante a navegação no cliente. A configuração tem de tratar navegadores comuns (Chrome no Android,
 * Safari no iOS) como agentes de metadados bloqueantes.
 */
const userAgents = {
  "Chrome mobile (Android)":
    "Mozilla/5.0 (Linux; Android 14; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/141.0.0.0 Mobile Safari/537.36",
  "Safari iOS (iPhone)":
    "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
};

describe("next.config — metadados bloqueantes", () => {
  it.each(Object.entries(userAgents))(
    "SPEC-0015:UT-01 htmlLimitedBots casa com o user agent do %s",
    (_name, userAgent) => {
      const bots = nextConfig.htmlLimitedBots;

      expect(bots, "htmlLimitedBots não configurado").toBeInstanceOf(RegExp);
      expect((bots as RegExp).test(userAgent)).toBe(true);
    },
  );
});

/**
 * SPEC-0041 UT-02 — o `next dev` só serve os recursos de desenvolvimento (`/_next/*`) para as origens de `localhost` e
 * as de `allowedDevOrigins`. A lista vem de `SUBSOLO_DEV_ORIGINS` (hosts separados por vírgula, só do servidor), que o
 * AppHost define quando recebe `Subsolo:PublicUrl` (o endereço do túnel). Sem a variável, ou vazia, a chave não é
 * declarada. O `next.config.ts` lê o ambiente na importação: cada caso recarrega o módulo depois de definir a variável.
 */
describe("next.config — origens do next dev (SUBSOLO_DEV_ORIGINS)", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.resetModules();
  });

  async function loadConfig() {
    vi.resetModules();
    return (await import("../next.config")).default;
  }

  it("SPEC-0041:UT-02 declara allowedDevOrigins com os hosts de SUBSOLO_DEV_ORIGINS", async () => {
    vi.stubEnv("SUBSOLO_DEV_ORIGINS", "a.example,b.example");

    const config = await loadConfig();

    expect(config.allowedDevOrigins).toEqual(["a.example", "b.example"]);
  });

  it("SPEC-0041:UT-02 não declara allowedDevOrigins sem SUBSOLO_DEV_ORIGINS", async () => {
    vi.stubEnv("SUBSOLO_DEV_ORIGINS", undefined);

    const config = await loadConfig();

    expect(
      Object.hasOwn(config, "allowedDevOrigins"),
      "allowedDevOrigins declarado sem a variável",
    ).toBe(false);
  });

  it("SPEC-0041:UT-02 não declara allowedDevOrigins com SUBSOLO_DEV_ORIGINS vazia", async () => {
    vi.stubEnv("SUBSOLO_DEV_ORIGINS", "");

    const config = await loadConfig();

    expect(
      Object.hasOwn(config, "allowedDevOrigins"),
      "allowedDevOrigins declarado com a variável vazia",
    ).toBe(false);
  });
});
