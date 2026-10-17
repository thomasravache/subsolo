// @vitest-environment node
import { describe, expect, it } from "vitest";

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
