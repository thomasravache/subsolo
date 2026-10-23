// @vitest-environment node
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0034 UT-01 — o `next@16.4.0` tem três avisos de segurança (GHSA-2464-fhr4-w9xw, GHSA-673q-mjm6-r5xr,
 * GHSA-p3m5-59mp-jv5r) corrigidos a partir da 16.4.1. A versão instalada do `next` tem de ser pelo menos a 16.4.1, e o
 * `eslint-config-next` tem de acompanhar a mesma versão do `next`.
 *
 * A versão do `next` vem do pacote instalado (`next/package.json`). A do `eslint-config-next` vem do
 * `web/package.json`, porque o pacote não exporta o próprio `package.json`; as versões são exatas (ADR-0009).
 */
const MIN_NEXT_VERSION = "16.4.1";

const requireFromWeb = createRequire(import.meta.url);

function installedNextVersion(): string {
  return (requireFromWeb("next/package.json") as { version: string }).version;
}

function declaredEslintConfigNextVersion(): string | undefined {
  const pkg = JSON.parse(
    readFileSync(new URL("../package.json", import.meta.url), "utf8"),
  ) as {
    devDependencies?: Record<string, string>;
  };
  return pkg.devDependencies?.["eslint-config-next"];
}

/** Compara `major.minor.patch` numericamente; pré-release e build são ignorados. */
function compareVersions(a: string, b: string): number {
  const parse = (v: string) =>
    (v.split(/[-+]/)[0] ?? v).split(".").map((part) => Number(part));
  const [pa, pb] = [parse(a), parse(b)];
  for (let i = 0; i < 3; i++) {
    const diff = (pa[i] ?? 0) - (pb[i] ?? 0);
    if (diff !== 0) return diff;
  }
  return 0;
}

describe("versão do Next.js — avisos de segurança", () => {
  it("SPEC-0034:UT-01 next é pelo menos 16.4.1 e eslint-config-next tem a mesma versão", () => {
    const next = installedNextVersion();

    expect(next, "versão do next ilegível").toMatch(/^\d+\.\d+\.\d+/);
    expect(
      compareVersions(next, MIN_NEXT_VERSION),
      `next@${next} está abaixo de ${MIN_NEXT_VERSION}, a primeira versão com a correção dos avisos`,
    ).toBeGreaterThanOrEqual(0);
    expect(
      declaredEslintConfigNextVersion(),
      "eslint-config-next deve ter a mesma versão do next",
    ).toBe(next);
  });
});
