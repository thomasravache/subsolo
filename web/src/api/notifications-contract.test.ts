// @vitest-environment node
import { spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

/**
 * SPEC-0030 CT-01 — o snapshot `docs/api/openapi.v1.json` (de onde sai o cliente TypeScript) publica os endpoints de
 * avisos do §6:
 * - `GET /api/v1/push/public-key`: público (sem 401), 200 `{publicKey}` e 429;
 * - `POST /api/v1/me/push-subscriptions` (corpo `{endpoint, keys: {p256dh, auth}}`): 204 sem corpo, 400
 *   `notifications.invalid_subscription`, 401, 400 do anti-forgery e 429; `DELETE` (corpo `{endpoint}`): 204, 401, 400 do
 *   anti-forgery e 429;
 * - `GET /api/v1/me/notification-preferences`: 200 `{newShowPush, dailyDigestEmail, reminderPush, reminderEmail,
 *   pushDevices}` (quatro booleanos e um inteiro), 401 e 429; `PUT` com os quatro booleanos: 200 com o mesmo corpo, 401,
 *   400 do anti-forgery e 429;
 * - `POST /api/v1/notifications/unsubscribe` (corpo `{token}`): público (sem 401), 204, 400 `notifications.invalid_token`,
 *   400 do anti-forgery e 429;
 * - o cliente `src/api/schema.d.ts` foi regenerado com as rotas e `(cd web && pnpm api:check)` passa. (As assinaturas de
 *   `INotificationsApi`, `Notice` e `IRecipientDirectory` são conferidas nos testes unitários do Notifications.)
 */

type Schema = {
  $ref?: string;
  type?: string | string[];
  format?: string;
  items?: Schema;
  required?: string[];
  properties?: Record<string, Schema>;
  oneOf?: Schema[];
  anyOf?: Schema[];
  allOf?: Schema[];
  additionalProperties?: Schema | boolean;
};

type Response = {
  description?: string;
  content?: Record<string, { schema?: Schema }>;
};

type Parameter = {
  name: string;
  in: string;
  required?: boolean;
  schema?: Schema;
};

type Operation = {
  security?: unknown[];
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

function resolve(schema: Schema | undefined): Schema {
  let current = schema ?? {};
  for (let depth = 0; current.$ref && depth < 10; depth++) {
    const name = current.$ref.replace("#/components/schemas/", "");
    current = document.components?.schemas?.[name] ?? {};
  }
  return current;
}

function property(schema: Schema, name: string): Schema | undefined {
  const resolved = resolve(schema);
  const direct = resolved.properties?.[name];
  if (direct) {
    return resolve(direct);
  }
  for (const part of [
    ...(resolved.allOf ?? []),
    ...(resolved.oneOf ?? []),
    ...(resolved.anyOf ?? []),
  ]) {
    const found = property(part, name);
    if (found) {
      return found;
    }
  }
  return undefined;
}

function types(schema: Schema): string[] {
  const own = schema.type
    ? Array.isArray(schema.type)
      ? schema.type
      : [schema.type]
    : [];
  const alternatives = [...(schema.oneOf ?? []), ...(schema.anyOf ?? [])]
    .map(resolve)
    .flatMap(types);
  return [...own, ...alternatives];
}

function operation(path: string, method: string): Operation {
  const found = document.paths[path]?.[method];
  expect(
    found,
    `${method.toUpperCase()} ${path} ausente no snapshot`,
  ).toBeDefined();
  return found as Operation;
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

/** O objeto tem exatamente as propriedades `names` (sem exigir o tipo de cada uma). */
function expectKeys(schema: Schema, names: string[], where: string) {
  expect(Object.keys(resolve(schema).properties ?? {}).sort(), where).toEqual(
    [...names].sort(),
  );
}

/** Os erros comuns das rotas de fã (§6): 401, 429 e, nas mutações, o 400 do anti-forgery. */
function expectFanErrors(path: string, method: string) {
  expectProblem(path, method, "401", ["auth.required"]);
  expectProblem(path, method, "429", ["security.rate_limited"]);
  if (method !== "get") {
    expectProblem(path, method, "400", ["security.csrf_invalid"]);
  }
}

/** Os erros comuns das mutações de fã (§6): 401, 429 e o 400 do anti-forgery. */
function expectMutationErrors(path: string, method: string) {
  expectFanErrors(path, method);
  expectProblem(path, method, "400", ["security.csrf_invalid"]);
}

/** 204 sem corpo. */
function expectNoContent(path: string, method: string) {
  const response = operation(path, method).responses["204"];
  expect(
    response,
    `${method.toUpperCase()} ${path} 204 não declarado`,
  ).toBeDefined();
  expect(
    Object.keys(response?.content ?? {}),
    `${method.toUpperCase()} ${path} 204 com corpo`,
  ).toEqual([]);
}

const publicKeyPath = "/api/v1/push/public-key";
const subscriptionsPath = "/api/v1/me/push-subscriptions";
const preferencesPath = "/api/v1/me/notification-preferences";
const unsubscribePath = "/api/v1/notifications/unsubscribe";

const preferenceFlags = [
  "newShowPush",
  "dailyDigestEmail",
  "reminderPush",
  "reminderEmail",
];

function requestBody(path: string, method: string): Schema {
  const content = operation(path, method).requestBody?.content?.[
    "application/json"
  ];
  expect(
    content,
    `${method.toUpperCase()} ${path} sem corpo application/json`,
  ).toBeDefined();
  return resolve(content?.schema);
}

function expectPublic(path: string, method: string) {
  expect(
    operation(path, method).responses["401"],
    `${method.toUpperCase()} ${path} é público (sem 401)`,
  ).toBeUndefined();
}

function expectPreferences(
  schema: Schema,
  withDevices: boolean,
  where: string,
) {
  expectKeys(
    schema,
    withDevices ? [...preferenceFlags, "pushDevices"] : preferenceFlags,
    where,
  );
  for (const flag of preferenceFlags) {
    expect(types(property(schema, flag) ?? {}), `${where}: ${flag}`).toContain(
      "boolean",
    );
  }
  if (withDevices) {
    expect(
      types(property(schema, "pushDevices") ?? {}),
      `${where}: pushDevices`,
    ).toContain("integer");
  }
}

describe("OpenAPI dos avisos fiel ao contrato §6", () => {
  it("SPEC-0030:CT-01 GET /api/v1/push/public-key é público e devolve {publicKey}", () => {
    const body = jsonResponse(publicKeyPath, "get", "200");
    expectKeys(body, ["publicKey"], "GET /push/public-key 200");
    expect(types(property(body, "publicKey") ?? {})).toContain("string");
    expectPublic(publicKeyPath, "get");
    expectProblem(publicKeyPath, "get", "429", ["security.rate_limited"]);
  });

  it("SPEC-0030:CT-01 POST /api/v1/me/push-subscriptions recebe {endpoint, keys: {p256dh, auth}} e responde 204 ou 400 invalid_subscription", () => {
    const body = requestBody(subscriptionsPath, "post");
    expectKeys(body, ["endpoint", "keys"], "POST /me/push-subscriptions corpo");
    expect(types(property(body, "endpoint") ?? {})).toContain("string");
    expectKeys(
      property(body, "keys") ?? {},
      ["p256dh", "auth"],
      "POST /me/push-subscriptions keys",
    );
    expectNoContent(subscriptionsPath, "post");
    expectProblem(subscriptionsPath, "post", "400", [
      "notifications.invalid_subscription",
      "security.csrf_invalid",
    ]);
    expectMutationErrors(subscriptionsPath, "post");
  });

  it("SPEC-0030:CT-01 DELETE /api/v1/me/push-subscriptions recebe {endpoint} e responde 204", () => {
    const body = requestBody(subscriptionsPath, "delete");
    expectKeys(body, ["endpoint"], "DELETE /me/push-subscriptions corpo");
    expectNoContent(subscriptionsPath, "delete");
    expectMutationErrors(subscriptionsPath, "delete");
  });

  it("SPEC-0030:CT-01 GET /api/v1/me/notification-preferences devolve os quatro booleanos e pushDevices", () => {
    expectPreferences(
      jsonResponse(preferencesPath, "get", "200"),
      true,
      "GET /me/notification-preferences 200",
    );
    expectFanErrors(preferencesPath, "get");
  });

  it("SPEC-0030:CT-01 PUT /api/v1/me/notification-preferences recebe os quatro booleanos e devolve 200 com o mesmo corpo", () => {
    expectPreferences(
      requestBody(preferencesPath, "put"),
      false,
      "PUT /me/notification-preferences corpo",
    );
    expectPreferences(
      jsonResponse(preferencesPath, "put", "200"),
      true,
      "PUT /me/notification-preferences 200",
    );
    expectMutationErrors(preferencesPath, "put");
  });

  it("SPEC-0030:CT-01 POST /api/v1/notifications/unsubscribe é público, recebe {token} e responde 204 ou 400 invalid_token", () => {
    const body = requestBody(unsubscribePath, "post");
    expectKeys(body, ["token"], "POST /notifications/unsubscribe corpo");
    expect(types(property(body, "token") ?? {})).toContain("string");
    expectNoContent(unsubscribePath, "post");
    expectProblem(unsubscribePath, "post", "400", [
      "notifications.invalid_token",
      "security.csrf_invalid",
    ]);
    expectProblem(unsubscribePath, "post", "429", ["security.rate_limited"]);
    expectPublic(unsubscribePath, "post");
  });

  it("SPEC-0030:CT-01 o cliente src/api/schema.d.ts foi regenerado com as rotas de avisos", () => {
    const client = readFileSync(clientPath, "utf8");
    for (const path of [
      publicKeyPath,
      subscriptionsPath,
      preferencesPath,
      unsubscribePath,
    ]) {
      expect(client, `${path} ausente no cliente`).toContain(`"${path}": {`);
    }
  });

  it("SPEC-0030:CT-01 pnpm api:check passa com o cliente regenerado", () => {
    const run = spawnSync("pnpm", ["run", "api:check"], {
      cwd: webDir,
      encoding: "utf8",
      env: process.env,
      timeout: 180_000,
    });

    expect(run.status, `${run.stdout}\n${run.stderr}`).toBe(0);
  }, 180_000);
});
