// @vitest-environment node
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0026 H24-06 (revisão da SPEC-0024) — um só helper de data e hora no fuso da cidade em `components/shows`: hoje
 * `ShowPage.tsx`, `show-metadata.ts` e o `showDateParts` de `messages.ts` montam cada um o seu
 * `Intl.DateTimeFormat`. Depois da mudança, só um arquivo (o helper) cria o formatador, e a página do show, os
 * metadados e "Meus shows" usam esse helper.
 */
const showsDir = fileURLToPath(new URL("..", import.meta.url));

function sources(): { file: string; text: string }[] {
  return readdirSync(showsDir, { withFileTypes: true })
    .filter(
      (entry) =>
        entry.isFile() &&
        /\.(ts|tsx)$/.test(entry.name) &&
        !/\.test\.(ts|tsx)$/.test(entry.name),
    )
    .map((entry) => ({
      file: entry.name,
      text: readFileSync(join(showsDir, entry.name), "utf8"),
    }));
}

describe("helper de data e hora no fuso da cidade", () => {
  it("SPEC-0026:H24-06 só um arquivo de components/shows cria o Intl.DateTimeFormat", () => {
    const formatters = sources()
      .filter(({ text }) => /\bIntl\.DateTimeFormat\s*\(/.test(text))
      .map(({ file }) => file);

    expect(
      formatters,
      `arquivos com o próprio Intl.DateTimeFormat: ${formatters.join(", ")}`,
    ).toHaveLength(1);
  });

  it("SPEC-0026:H24-06 a página do show, os metadados e Meus shows não formatam a data por conta própria", () => {
    const own = sources()
      .filter(({ file }) =>
        ["ShowPage.tsx", "show-metadata.ts", "MyShowsList.tsx"].includes(file),
      )
      .filter(({ text }) => /\bIntl\.DateTimeFormat\s*\(/.test(text))
      .map(({ file }) => file);

    expect(own).toEqual([]);
  });
});
