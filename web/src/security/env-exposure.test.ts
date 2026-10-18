// @vitest-environment node
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0016 UT-05 — nenhuma variável de ambiente chega ao navegador pelo código: nos arquivos de `web/src/**` (fora os
 * testes) e no `web/next.config.ts`, ninguém usa `NEXT_PUBLIC_`, nenhum arquivo com `"use client"` lê `process.env`,
 * e o `next.config.ts` não declara `env` nem `publicRuntimeConfig`. A própria varredura reprova os exemplos ruins
 * (um componente cliente com `process.env.X`, um `NEXT_PUBLIC_` e um `next.config` com `env`).
 */

const webDir = fileURLToPath(new URL("../..", import.meta.url));
const srcDir = join(webDir, "src");
const nextConfigPath = join(webDir, "next.config.ts");

const sourceExtensions = /\.(?:ts|tsx|js|jsx|mjs|cjs)$/;
const testFile = /\.test\.(?:ts|tsx|js|jsx)$/;

/** A diretiva em si, logo no ponto em que o código começa. */
const useClientDirective = /^(["'])use client\1/;

/**
 * Diretiva `"use client"` no topo do arquivo (depois só de espaços e comentários). Varredura linear: pula espaços,
 * comentários de linha e de bloco com `indexOf`, sem regex com quantificadores aninhados (sem backtracking).
 */
function startsWithUseClient(text: string): boolean {
  let at = 0;
  while (at < text.length) {
    if (/\s/.test(text[at] ?? "")) {
      at += 1;
    } else if (text.startsWith("//", at)) {
      const end = text.indexOf("\n", at);
      if (end < 0) {
        return false;
      }
      at = end + 1;
    } else if (text.startsWith("/*", at)) {
      const end = text.indexOf("*/", at + 2);
      if (end < 0) {
        return false;
      }
      at = end + 2;
    } else {
      return useClientDirective.test(text.slice(at));
    }
  }
  return false;
}

/** Leitura do ambiente: `process.env`, `process["env"]` ou `process['env']`. */
const processEnv = /\bprocess\s*(?:\.\s*env\b|\[\s*(["'])env\1\s*\])/;

const nextPublic = /NEXT_PUBLIC_/;

/** Chaves do `next.config` que embutem valores do ambiente no código do navegador. */
const exposingConfig = /\b(?:env|publicRuntimeConfig)\s*:/;

interface Finding {
  file: string;
  rule: string;
}

/** Regras para um arquivo de código do web. */
function scanSource(file: string, text: string): Finding[] {
  const findings: Finding[] = [];
  if (nextPublic.test(text)) {
    findings.push({ file, rule: "usa NEXT_PUBLIC_" });
  }
  if (startsWithUseClient(text) && processEnv.test(text)) {
    findings.push({ file, rule: '"use client" lendo process.env' });
  }
  return findings;
}

/** Regras para o `next.config`. */
function scanNextConfig(file: string, text: string): Finding[] {
  const findings = scanSource(file, text);
  if (exposingConfig.test(text)) {
    findings.push({
      file,
      rule: "next.config declara env/publicRuntimeConfig",
    });
  }
  return findings;
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) {
      return sourceFiles(path);
    }
    return sourceExtensions.test(name) && !testFile.test(name) ? [path] : [];
  });
}

describe("variáveis de ambiente fora do navegador", () => {
  it("SPEC-0016:UT-05 nenhum arquivo de web/src usa NEXT_PUBLIC_ nem lê process.env em componente cliente", () => {
    const files = sourceFiles(srcDir);
    expect(files.length).toBeGreaterThan(0);

    const findings = files.flatMap((path) =>
      scanSource(relative(webDir, path), readFileSync(path, "utf8")),
    );

    expect(findings).toEqual([]);
  });

  it("SPEC-0016:UT-05 o next.config.ts não declara env nem publicRuntimeConfig", () => {
    expect(
      scanNextConfig("next.config.ts", readFileSync(nextConfigPath, "utf8")),
    ).toEqual([]);
  });

  it("SPEC-0016:UT-05 a varredura reprova um componente cliente que lê process.env.X", () => {
    const sample = `"use client";\n\nexport function Bad() {\n  return <p>{process.env.X}</p>;\n}\n`;

    expect(scanSource("exemplo/Bad.tsx", sample)).toEqual([
      { file: "exemplo/Bad.tsx", rule: '"use client" lendo process.env' },
    ]);
  });

  it("SPEC-0016:UT-05 a varredura reprova process['env'] depois de comentários no topo do componente cliente", () => {
    const sample = `// comentário\n/* outro */\n'use client';\nconst url = process['env'].API_URL;\n`;

    expect(scanSource("exemplo/Bad2.tsx", sample)).toHaveLength(1);
  });

  it("SPEC-0016:UT-05 a varredura reprova NEXT_PUBLIC_ e o env do next.config", () => {
    expect(
      scanSource("exemplo/a.ts", "const u = process.env.NEXT_PUBLIC_API;"),
    ).toEqual([{ file: "exemplo/a.ts", rule: "usa NEXT_PUBLIC_" }]);
    expect(
      scanNextConfig(
        "next.config.ts",
        "const c = { env: { API_URL: process.env.API_URL } };",
      ),
    ).toEqual([
      {
        file: "next.config.ts",
        rule: "next.config declara env/publicRuntimeConfig",
      },
    ]);
    expect(
      scanNextConfig(
        "next.config.ts",
        "const c = { publicRuntimeConfig: {} };",
      ),
    ).toHaveLength(1);
  });

  it("SPEC-0016:UT-05 a varredura do topo é linear (comentários aninhados em série não travam)", () => {
    const sample = `/*${"*//*".repeat(50_000)}\nconst url = process.env.API_URL;\n`;

    const started = performance.now();
    expect(scanSource("exemplo/longo.ts", sample)).toEqual([]);
    expect(performance.now() - started).toBeLessThan(1_000);
  });

  it("SPEC-0016:UT-05 código de servidor pode ler process.env", () => {
    expect(
      scanSource(
        "exemplo/server.ts",
        "export const url = () => process.env.API_URL;",
      ),
    ).toEqual([]);
  });
});
