// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0023 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica o cadastro de show
 * como no §6:
 * - `POST /api/v1/shows`: corpo `{organizerProfileId, title, city, startsLocal, venue, lineup, styles, price,
 *   ticketUrl, flyerId}`; 201 `{id, slug}`; 400 `events.invalid_show`, `events.flyer_not_found` e
 *   `security.csrf_invalid`; 401 `auth.required`; 403 `auth.forbidden`; 409 `events.show_slug_taken`; 429
 *   `security.rate_limited`;
 * - `PUT /api/v1/shows/{id}`: o mesmo corpo; 200 `{id, slug}`; 400; 403; 404 `events.show_not_found`;
 * - `POST /api/v1/shows/{id}/status`: corpo `{status}` (`scheduled | cancelled | postponed`); 200; 400
 *   `events.invalid_status`; 403; 404 `events.show_not_found`;
 * - `GET /api/v1/me/shows?period=upcoming|past`: 200 com a lista `[{id, slug, title, startsAt, city, venueName, status,
 *   flyerThumbUrl, organizerProfileId}]`; 401; 403; 429. SPEC-0024 (H23-06): cada item ganha `timeZone` (aditivo).
 * O cliente `src/api/schema.d.ts` foi regenerado com as rotas e `(cd web && pnpm api:check)` passa. Os eventos públicos
 * (`ShowPublishedIntegrationEvent`, `ShowChangedIntegrationEvent`) são conferidos no
 * `Subsolo.Events.UnitTests/ShowIntegrationEventsContractTests.cs`.
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

type Parameter = { name: string; in: string; schema?: Schema };

type Operation = {
  parameters?: Parameter[];
  requestBody?: { content?: Record<string, { schema?: Schema }> };
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

const showsPath = "/api/v1/shows";
const showPath = "/api/v1/shows/{id}";
const statusPath = "/api/v1/shows/{id}/status";
const myShowsPath = "/api/v1/me/shows";

const showBody = [
  "organizerProfileId",
  "title",
  "city",
  "startsLocal",
  "venue",
  "lineup",
  "styles",
  "price",
  "ticketUrl",
  "flyerId",
];

const myShow = [
  "id",
  "slug",
  "title",
  "startsAt",
  "city",
  "venueName",
  "status",
  "flyerThumbUrl",
  "organizerProfileId",
];

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

function properties(schema: Schema): string[] {
  const resolved = resolve(schema);
  return [
    ...Object.keys(resolved.properties ?? {}),
    ...[
      ...(resolved.allOf ?? []),
      ...(resolved.oneOf ?? []),
      ...(resolved.anyOf ?? []),
    ].flatMap(properties),
  ];
}

function operation(path: string, method: string): Operation {
  const found = document.paths[path]?.[method];
  expect(
    found,
    `${method.toUpperCase()} ${path} ausente no snapshot`,
  ).toBeDefined();
  return found as Operation;
}

function jsonBody(path: string, method: string): Schema {
  const content = operation(path, method).requestBody?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} sem corpo application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

function jsonResponse(path: string, method: string, status: string): Schema {
  const content = operation(path, method).responses[status]?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} ${status} sem application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

/** A resposta de erro é ProblemDetails e cita cada código (`code=…`) na descrição. */
function expectProblem(
  path: string,
  method: string,
  status: string,
  codes: string[],
) {
  const response = operation(path, method).responses[status];
  const where = `${method.toUpperCase()} ${path} ${status}`;
  expect(response, `${where} não declarado`).toBeDefined();
  expect(
    response?.content?.["application/problem+json"],
    `${where} sem application/problem+json`,
  ).toBeDefined();
  for (const code of codes) {
    expect(response?.description ?? "", where).toContain(`code=${code}`);
  }
}

describe("OpenAPI do cadastro de show fiel ao contrato §6", () => {
  it("SPEC-0023:CT-01 POST /api/v1/shows recebe o show e responde 201 {id, slug}", () => {
    expect(properties(jsonBody(showsPath, "post")).sort()).toEqual(
      expect.arrayContaining(showBody),
    );
    expect(properties(jsonResponse(showsPath, "post", "201")).sort()).toEqual([
      "id",
      "slug",
    ]);
  });

  it("SPEC-0023:CT-01 POST /api/v1/shows declara 400, 401, 403, 409 e 429 com os códigos do §6", () => {
    expectProblem(showsPath, "post", "400", [
      "events.invalid_show",
      "events.flyer_not_found",
      "security.csrf_invalid",
    ]);
    expectProblem(showsPath, "post", "401", ["auth.required"]);
    expectProblem(showsPath, "post", "403", ["auth.forbidden"]);
    expectProblem(showsPath, "post", "409", ["events.show_slug_taken"]);
    expectProblem(showsPath, "post", "429", ["security.rate_limited"]);
  });

  it("SPEC-0023:CT-01 PUT /api/v1/shows/{id} recebe o mesmo corpo e responde 200 {id, slug}, 400, 403 e 404", () => {
    expect(properties(jsonBody(showPath, "put")).sort()).toEqual(
      expect.arrayContaining(showBody),
    );
    expect(properties(jsonResponse(showPath, "put", "200")).sort()).toEqual([
      "id",
      "slug",
    ]);
    expectProblem(showPath, "put", "400", ["events.invalid_show"]);
    expectProblem(showPath, "put", "401", ["auth.required"]);
    expectProblem(showPath, "put", "403", ["auth.forbidden"]);
    expectProblem(showPath, "put", "404", ["events.show_not_found"]);
  });

  it("SPEC-0023:CT-01 POST /api/v1/shows/{id}/status recebe {status} e declara 200, 400, 403 e 404", () => {
    const body = jsonBody(statusPath, "post");
    expect(properties(body)).toContain("status");
    const status = resolve(resolve(body).properties?.["status"]);
    if (status.enum) {
      expect([...status.enum].sort()).toEqual([
        "cancelled",
        "postponed",
        "scheduled",
      ]);
    }
    expect(
      operation(statusPath, "post").responses["200"],
      "POST status sem 200",
    ).toBeDefined();
    expectProblem(statusPath, "post", "400", ["events.invalid_status"]);
    expectProblem(statusPath, "post", "403", ["auth.forbidden"]);
    expectProblem(statusPath, "post", "404", ["events.show_not_found"]);
  });

  it("SPEC-0023:CT-01 GET /api/v1/me/shows?period= responde a lista com os campos do §6", () => {
    const period = operation(myShowsPath, "get").parameters?.find(
      (parameter) => parameter.name === "period",
    );
    expect(period, "GET /me/shows sem o parâmetro period").toBeDefined();
    expect(period?.in).toBe("query");
    const list = jsonResponse(myShowsPath, "get", "200");
    expect(list.type).toBe("array");
    // SPEC-0024 (H23-06): o item ganhou campo aditivo (timeZone); aqui ficam os campos da SPEC-0023.
    expect(properties(list.items ?? {})).toEqual(
      expect.arrayContaining(myShow),
    );
    expectProblem(myShowsPath, "get", "401", ["auth.required"]);
    expectProblem(myShowsPath, "get", "403", ["auth.forbidden"]);
    expectProblem(myShowsPath, "get", "429", ["security.rate_limited"]);
  });

  it("SPEC-0024:H23-06 cada item de GET /api/v1/me/shows traz o timeZone da cidade (campo aditivo)", () => {
    const list = jsonResponse(myShowsPath, "get", "200");
    const item = resolve(list.items ?? {});
    expect(properties(item).sort()).toEqual([...myShow, "timeZone"].sort());
    expect(item.required ?? [], "timeZone opcional no item").toContain(
      "timeZone",
    );
    expect(resolve(item.properties?.["timeZone"]).type).toBe("string");
  });

  it("SPEC-0023:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de show", () => {
    const client = readFileSync(clientPath, "utf8");

    for (const path of [showsPath, showPath, statusPath, myShowsPath]) {
      expect(client).toContain(`"${path}"`);
    }
  });

  it("SPEC-0023:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
