// @vitest-environment node
import { existsSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0011 UT-01 — os tokens de cor de `web/src/styles/tokens.css` (bloco `@theme` do Tailwind 4) atendem ao
 * contraste WCAG 2.2 AA no tema escuro: ≥ 4,5:1 para texto e ≥ 3:1 para componentes.
 *
 * Os pares verificados são:
 * - os obrigatórios abaixo (texto e texto secundário sobre os três fundos; "Cancelado", "Adiado", destaque e foco
 *   sobre o fundo);
 * - todos os pares declarados em `docs/design/DESIGN.md`: cada linha de tabela com dois tokens `--color-*` entre
 *   crases (primeiro o da frente, depois o do fundo) e o mínimo exigido como `4,5:1` (ou `4.5:1`) ou `3:1`.
 *   Ex.: `| Texto do botão primário | \`--color-text-on-accent\` | \`--color-accent\` | 4,5:1 |`.
 *
 * As cores são lidas como hexadecimais (`#rgb`/`#rrggbb`), inclusive via `var(--outro-token)` no mesmo arquivo.
 */

const tokensPath = fileURLToPath(
  new URL("../../../styles/tokens.css", import.meta.url),
);
const designPath = fileURLToPath(
  new URL("../../../../../docs/design/DESIGN.md", import.meta.url),
);

const TEXT = 4.5;
const COMPONENT = 3;

/** Pares que o tema precisa garantir, independentemente do que o DESIGN.md declarar. */
const requiredPairs: ReadonlyArray<[string, string, number, string]> = [
  ["--color-text", "--color-bg", TEXT, "texto sobre o fundo"],
  ["--color-text", "--color-surface", TEXT, "texto sobre a superfície"],
  [
    "--color-text",
    "--color-surface-raised",
    TEXT,
    "texto sobre a superfície elevada",
  ],
  ["--color-text-muted", "--color-bg", TEXT, "texto secundário sobre o fundo"],
  [
    "--color-text-muted",
    "--color-surface",
    TEXT,
    "texto secundário sobre a superfície",
  ],
  [
    "--color-text-muted",
    "--color-surface-raised",
    TEXT,
    "texto secundário sobre a superfície elevada",
  ],
  ["--color-danger", "--color-bg", TEXT, "“Cancelado” sobre o fundo"],
  ["--color-warning", "--color-bg", TEXT, "“Adiado” sobre o fundo"],
  ["--color-accent", "--color-bg", COMPONENT, "destaque sobre o fundo"],
  ["--color-focus", "--color-bg", COMPONENT, "indicador de foco sobre o fundo"],
];

/** Tokens do contrato §6 (cores nomeadas; fontes, raios e espaçamentos por prefixo). */
const contractColors = [
  "--color-bg",
  "--color-surface",
  "--color-surface-raised",
  "--color-text",
  "--color-text-muted",
  "--color-accent",
  "--color-danger",
  "--color-warning",
  "--color-border",
  "--color-focus",
  "--font-display",
  "--font-body",
];
const contractPrefixes = ["--radius-", "--space-"];

/** Paleta do design system "Subsolo Dark" aprovado no Stitch. */
const approvedPalette: Record<string, string> = {
  "--color-bg": "#0b0b0c",
  "--color-text": "#e8e2d4",
  "--color-accent": "#d7263d",
  "--color-warning": "#f2a93b",
  "--color-danger": "#ff5a5f",
};

function readTokens(): string {
  expect(
    existsSync(tokensPath),
    `tokens ausentes: ${tokensPath} (Tailwind 4, bloco @theme)`,
  ).toBe(true);
  return readFileSync(tokensPath, "utf8");
}

/** Conteúdo dos blocos `@theme` (com ou sem modificadores, ex.: `@theme inline`). */
function themeBlocks(css: string): string {
  const blocks: string[] = [];
  const opener = /@theme\b[^{]*\{/g;
  for (let match = opener.exec(css); match; match = opener.exec(css)) {
    let depth = 1;
    let index = match.index + match[0].length;
    const start = index;
    while (index < css.length && depth > 0) {
      if (css[index] === "{") depth++;
      if (css[index] === "}") depth--;
      index++;
    }
    blocks.push(css.slice(start, index - 1));
  }
  return blocks.join("\n");
}

/** Declarações `--nome: valor;` do texto, sem comentários. */
function declarations(css: string): Map<string, string> {
  const result = new Map<string, string>();
  const withoutComments = css.replace(/\/\*[\s\S]*?\*\//g, "");
  for (const match of withoutComments.matchAll(/(--[\w-]+)\s*:\s*([^;]+);/g)) {
    result.set(match[1] as string, (match[2] as string).trim());
  }
  return result;
}

function themeTokens(): Map<string, string> {
  const css = readTokens();
  const theme = themeBlocks(css);
  expect(theme, "tokens.css sem bloco @theme").not.toBe("");
  return declarations(theme);
}

/** Valor hexadecimal (minúsculo, 6 dígitos) de um token de cor, seguindo `var(--x)`. */
function hex(tokens: Map<string, string>, name: string): string {
  let value = tokens.get(name);
  for (let depth = 0; value?.startsWith("var(") && depth < 10; depth++) {
    const inner = /^var\(\s*(--[\w-]+)/.exec(value)?.[1];
    value = inner ? tokens.get(inner) : undefined;
  }
  expect(value, `token ${name} sem valor em tokens.css`).toBeDefined();
  const color = (value as string).toLowerCase();
  const short = /^#([0-9a-f])([0-9a-f])([0-9a-f])$/.exec(color);
  if (short) {
    return `#${short[1]}${short[1]}${short[2]}${short[2]}${short[3]}${short[3]}`;
  }
  expect(color, `token ${name} não é uma cor hexadecimal: ${value}`).toMatch(
    /^#[0-9a-f]{6}$/,
  );
  return color;
}

/** Luminância relativa WCAG 2.x. */
function luminance(color: string): number {
  const channels = [1, 3, 5].map((offset) => {
    const value = parseInt(color.slice(offset, offset + 2), 16) / 255;
    return value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  const [r, g, b] = channels as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [
    number,
    number,
  ];
  return (light + 0.05) / (dark + 0.05);
}

/** Pares declarados nas tabelas do DESIGN.md. */
function designPairs(): Array<[string, string, number, string]> {
  expect(existsSync(designPath), `DESIGN.md ausente: ${designPath}`).toBe(true);
  const pairs: Array<[string, string, number, string]> = [];
  for (const line of readFileSync(designPath, "utf8").split("\n")) {
    if (!line.trim().startsWith("|")) continue;
    const tokens = [...line.matchAll(/`(--color-[\w-]+)`/g)].map(
      (match) => match[1] as string,
    );
    const minimum = /\b(4[,.]5|3)\s*:\s*1\b/.exec(line)?.[1];
    if (tokens.length === 2 && minimum) {
      pairs.push([
        tokens[0] as string,
        tokens[1] as string,
        Number(minimum.replace(",", ".")),
        line.trim(),
      ]);
    }
  }
  return pairs;
}

function failures(
  tokens: Map<string, string>,
  pairs: ReadonlyArray<readonly [string, string, number, string]>,
): string[] {
  return pairs.flatMap(([front, back, minimum, label]) => {
    const ratio = contrast(hex(tokens, front), hex(tokens, back));
    return ratio + 1e-9 >= minimum
      ? []
      : [
          `${front} sobre ${back} = ${ratio.toFixed(2)}:1 (mínimo ${minimum}:1) — ${label}`,
        ];
  });
}

describe("tokens de design do tema escuro", () => {
  it("SPEC-0011:UT-01 tokens.css declara no @theme todos os tokens do contrato", () => {
    const tokens = themeTokens();
    const names = [...tokens.keys()];

    const missing = [
      ...contractColors.filter((name) => !tokens.has(name)),
      ...contractPrefixes
        .filter((prefix) => !names.some((name) => name.startsWith(prefix)))
        .map((prefix) => `${prefix}*`),
    ];
    expect(missing, "tokens do contrato ausentes").toEqual([]);
  });

  it("SPEC-0011:UT-01 cores-base iguais às do design system aprovado no Stitch e cantos de 4px", () => {
    const tokens = themeTokens();

    for (const [name, expected] of Object.entries(approvedPalette)) {
      expect(hex(tokens, name), name).toBe(expected);
    }
    const radii = [...tokens].filter(([name]) => name.startsWith("--radius-"));
    expect(
      radii.some(([, value]) => /^(4px|0\.25rem)$/.test(value)),
      `nenhum --radius-* de 4px: ${radii.map(([n, v]) => `${n}: ${v}`).join(", ")}`,
    ).toBe(true);
  });

  it("SPEC-0011:UT-01 pares obrigatórios atingem 4,5:1 (texto) e 3:1 (componentes)", () => {
    const tokens = themeTokens();

    expect(failures(tokens, requiredPairs)).toEqual([]);
  });

  it("SPEC-0011:UT-01 pares declarados no DESIGN.md atingem o contraste mínimo declarado", () => {
    const tokens = themeTokens();
    const pairs = designPairs();

    expect(
      pairs.length,
      "DESIGN.md sem tabela de pares de contraste",
    ).toBeGreaterThan(0);
    expect(
      pairs.filter(([, , minimum]) => minimum < COMPONENT),
      "mínimo declarado abaixo de 3:1",
    ).toEqual([]);
    expect(failures(tokens, pairs)).toEqual([]);
  });
});
