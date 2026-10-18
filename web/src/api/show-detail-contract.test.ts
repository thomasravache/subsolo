// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0024 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica o detalhe do show
 * como no §6:
 * - `GET /api/v1/cities/{city}/shows/{slug}` (público): 200 `{id, slug, title, city: {slug, name, timeZone}, startsAt,
 *   venue: {name, profileSlug|null}, lineup: [{name, profileSlug|null}], styles: [{slug, name}], price, ticketUrl|null,
 *   status: scheduled|cancelled|postponed, flyer: {thumbUrl, fullUrl, ogUrl, width, height}}`;
 * - 404 em ProblemDetails com `events.show_not_found` e `events.city_not_found`; 503 em ProblemDetails (API sem banco);
 * - o cliente `src/api/schema.d.ts` foi regenerado com a rota e `(cd web && pnpm api:check)` passa.
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  enum?: unknown[];
  items?: Schema;
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Operation = {
  parameters?: { name: string; in: string }[];
  responses: Record<string, Response>;
};

type OpenApiDocument = {
  paths: Record<string, Record<string, Operation>>;
  components?: { schemas?: Record<string, Schema> };
};

const webDir = fileURLToPath(new URL("../..", import.meta.url));
const snapshotPath = fileURLToPath(
  new URL("../../../docs/api/openapi.v1.json", import.meta.url),
);
const clientPath = fileURLToPath(new URL("./schema.d.ts", import.meta.url));
const document = JSON.parse(
  readFileSync(snapshotPath, "utf8"),
) as OpenApiDocument;

const detailPath = "/api/v1/cities/{city}/shows/{slug}";

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

/** O esquema sem o `null` de um `oneOf`/`anyOf` anulável (`T | null`). */
function nonNull(schema: Schema | undefined): Schema {
  const resolved = resolve(schema);
  const options = [...(resolved.oneOf ?? []), ...(resolved.anyOf ?? [])]
    .map(resolve)
    .filter((option) => option.type !== "null");
  return options.length === 1 ? resolve(options[0]) : resolved;
}

function propertiesOf(schema: Schema | undefined): string[] {
  return Object.keys(nonNull(schema).properties ?? {}).sort();
}

function property(schema: Schema, name: string): Schema {
  const found = nonNull(schema).properties?.[name];
  expect(found, `propriedade ${name} ausente`).toBeDefined();
  return found as Schema;
}

/** O tipo admite `null` (`type: [..., "null"]` ou `oneOf/anyOf` com `null`). */
function nullable(schema: Schema): boolean {
  const resolved = resolve(schema);
  const types = [resolved.type].flat();
  return (
    types.includes("null") ||
    [...(resolved.oneOf ?? []), ...(resolved.anyOf ?? [])].some(
      (option) => resolve(option).type === "null",
    )
  );
}

function detail(): Operation {
  const found = document.paths[detailPath]?.["get"];
  expect(found, `GET ${detailPath} ausente no snapshot`).toBeDefined();
  return found as Operation;
}

function okBody(): Schema {
  const content = detail().responses["200"]?.content?.["application/json"];
  expect(content, `GET ${detailPath} 200 sem application/json`).toBeDefined();
  return resolve(content?.schema);
}

describe("OpenAPI do detalhe do show fiel ao contrato §6", () => {
  it("SPEC-0024:CT-01 GET /api/v1/cities/{city}/shows/{slug} tem os parâmetros de caminho city e slug", () => {
    const names = (detail().parameters ?? [])
      .filter((parameter) => parameter.in === "path")
      .map((parameter) => parameter.name)
      .sort();
    expect(names).toEqual(["city", "slug"]);
  });

  it("SPEC-0024:CT-01 o 200 tem o corpo do §6", () => {
    const body = okBody();
    expect(propertiesOf(body)).toEqual(
      [
        "id",
        "slug",
        "title",
        "city",
        "startsAt",
        "venue",
        "lineup",
        "styles",
        "price",
        "ticketUrl",
        "status",
        "flyer",
      ].sort(),
    );
    expect(propertiesOf(property(body, "city"))).toEqual(
      ["name", "slug", "timeZone"].sort(),
    );
    const venue = property(body, "venue");
    expect(propertiesOf(venue)).toEqual(["name", "profileSlug"]);
    expect(nullable(property(venue, "profileSlug"))).toBe(true);
    const lineup = nonNull(property(body, "lineup"));
    expect(lineup.type).toBe("array");
    expect(propertiesOf(lineup.items)).toEqual(["name", "profileSlug"]);
    const styles = nonNull(property(body, "styles"));
    expect(styles.type).toBe("array");
    expect(propertiesOf(styles.items)).toEqual(["name", "slug"]);
    expect(nullable(property(body, "ticketUrl"))).toBe(true);
    const status = nonNull(property(body, "status"));
    expect([...(status.enum ?? [])].sort()).toEqual([
      "cancelled",
      "postponed",
      "scheduled",
    ]);
    expect(propertiesOf(property(body, "flyer"))).toEqual(
      ["fullUrl", "height", "ogUrl", "thumbUrl", "width"].sort(),
    );
  });

  it("SPEC-0024:CT-01 declara 404 com events.show_not_found e events.city_not_found, e 503, em ProblemDetails", () => {
    const notFound = detail().responses["404"];
    expect(notFound, "404 não declarado").toBeDefined();
    expect(notFound?.content?.["application/problem+json"]).toBeDefined();
    for (const code of ["events.show_not_found", "events.city_not_found"]) {
      expect(notFound?.description ?? "").toContain(`code=${code}`);
    }
    const unavailable = detail().responses["503"];
    expect(unavailable, "503 não declarado").toBeDefined();
    expect(unavailable?.content?.["application/problem+json"]).toBeDefined();
  });

  it("SPEC-0024:CT-01 o cliente src/api/schema.d.ts foi regenerado com a rota do detalhe", () => {
    expect(readFileSync(clientPath, "utf8")).toContain(`"${detailPath}"`);
  });

  it("SPEC-0024:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
