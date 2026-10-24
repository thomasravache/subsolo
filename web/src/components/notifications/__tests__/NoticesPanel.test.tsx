// @vitest-environment jsdom
import { createECDH, randomBytes } from "node:crypto";

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import type { NotificationPreferences } from "@/api/notifications";

/**
 * SPEC-0030 UT-03 — a parte "Avisos" da tela "Avisos e Seguindo" (Stitch 35b282364c47425c9e9cfe89e7b1c1e2, com os
 * ajustes da spec: dois interruptores de lembrete e a instrução do iPhone no bloco de alerta), com o navegador simulado
 * (`Notification`, `PushManager`, `navigator.serviceWorker`, `navigator.standalone`) e a API respondendo por um `fetch`
 * falso:
 * - permissão `default`: mostra "Ativar avisos" e só pede a permissão depois do toque (ADR-0013); concedida, inscreve o
 *   aparelho com a chave pública de `GET /api/v1/push/public-key`, grava em `POST /api/v1/me/push-subscriptions` (com
 *   `X-CSRF`) e anuncia "Avisos ativos neste aparelho";
 * - permissão `granted` com o aparelho inscrito: "Avisos ativos neste aparelho" e os quatro interruptores
 *   (`role="switch"`, `aria-checked`), cada mudança salva por `PUT /api/v1/me/notification-preferences`;
 * - permissão `denied` (ou negada no pedido): "Os avisos estão bloqueados no navegador.", nada gravado;
 * - sem suporte a push: sem o botão, com "Este navegador não recebe avisos." (Emenda de 2026-10-23); nada pedido nem gravado;
 * - iPhone sem instalar (`navigator.standalone` falso): "Adicione o Subsolo à tela de início para receber avisos",
 *   sem pedir permissão.
 *
 * SPEC-0030 UT-05 (Emenda da revisão G4, 2026-10-23) — o painel fora de sincronia com o servidor:
 * - com uma inscrição no navegador, ao abrir ele a registra de novo (`POST /api/v1/me/push-subscriptions`, idempotente)
 *   antes de mostrar "Avisos ativos neste aparelho";
 * - com a permissão já `denied` ao abrir (pelo `Notification.permission` ou pela Permissions API), mostra "Os avisos
 *   estão bloqueados no navegador." sem o botão "Ativar avisos";
 * - durante a ativação mostra `noticeMessages.activating` ("Ativando…") e o botão fica desabilitado.
 */

type Call = {
  method: string;
  path: string;
  headers: Headers;
  body: string;
};

const calls: Call[] = [];

/** Chave pública P-256 nova (65 bytes, ponto não comprimido, base64url), gerada no teste. */
function newPublicKey(): string {
  const ecdh = createECDH("prime256v1");
  ecdh.generateKeys();
  return ecdh.getPublicKey().toString("base64url");
}

/** Chave pública VAPID da API de teste (gerada aqui; nunca um segredo real). */
const publicKey = newPublicKey();

const subscriptionJson = {
  endpoint: "https://fcm.googleapis.com/fcm/send/aparelho-de-teste",
  keys: {
    p256dh: newPublicKey(),
    auth: randomBytes(16).toString("base64url"),
  },
};

const switchNames = [
  "Show novo de quem eu sigo (na hora)",
  "Resumo diário por e-mail",
  "Lembrete na véspera (no celular)",
  "Lembrete na véspera (por e-mail)",
] as const;

const initialPreferences: NotificationPreferences = {
  newShowPush: false,
  dailyDigestEmail: false,
  reminderPush: true,
  reminderEmail: false,
  pushDevices: 0,
};

const activePreferences: NotificationPreferences = {
  ...initialPreferences,
  newShowPush: true,
  pushDevices: 1,
};

function urlOf(input: Parameters<typeof fetch>[0]): URL {
  return new URL(
    typeof input === "string"
      ? input
      : input instanceof URL
        ? input.href
        : input.url,
    window.location.href,
  );
}

async function record(
  input: Parameters<typeof fetch>[0],
  init?: Parameters<typeof fetch>[1],
): Promise<Call> {
  const request =
    typeof input === "object" && "method" in input ? input : undefined;
  const method = (init?.method ?? request?.method ?? "GET").toUpperCase();
  const headers = new Headers(init?.headers ?? request?.headers);
  let body = "";
  if (typeof init?.body === "string") {
    body = init.body;
  } else if (request && method !== "GET" && method !== "HEAD") {
    body = await request.clone().text();
  }
  return { method, path: urlOf(input).pathname, headers, body };
}

/**
 * A API: chave pública, antiforgery, inscrição (204) e preferências (o PUT devolve o corpo com `pushDevices`). Com
 * `holdSubscriptionPost`, o `POST` da inscrição só responde quando a promessa resolver.
 */
function stubApi(
  preferences: NotificationPreferences,
  holdSubscriptionPost?: Promise<void>,
) {
  vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const call = await record(input, init);
    calls.push(call);
    if (call.path === "/api/v1/antiforgery") {
      return Response.json({ token: "csrf-token" });
    }
    if (call.path === "/api/v1/push/public-key") {
      return Response.json({ publicKey });
    }
    if (call.path === "/api/v1/me/push-subscriptions") {
      if (holdSubscriptionPost) {
        await holdSubscriptionPost;
      }
      return new Response(null, { status: 204 });
    }
    if (call.path === "/api/v1/me/notification-preferences") {
      if (call.method === "PUT") {
        return Response.json({
          ...(JSON.parse(call.body) as object),
          pushDevices: preferences.pushDevices,
        });
      }
      return Response.json(preferences);
    }
    throw new TypeError("fetch failed");
  });
}

interface BrowserOptions {
  permission?: NotificationPermission;
  /** O pedido de permissão responde isto. */
  answer?: NotificationPermission;
  /** Sem `Notification`/`PushManager`/`serviceWorker`. */
  unsupported?: boolean;
  /** O aparelho já está inscrito (`getSubscription` devolve a inscrição). */
  subscribed?: boolean;
  /** iPhone com o Safari (não instalado: `navigator.standalone` falso). */
  iosNotInstalled?: boolean;
}

const requestPermission = vi.fn<() => Promise<NotificationPermission>>();
const subscribe =
  vi.fn<(options: PushSubscriptionOptionsInit) => Promise<unknown>>();
const getSubscription = vi.fn<() => Promise<unknown>>();
const register = vi.fn<(url: string) => Promise<unknown>>();

function fakeSubscription() {
  return {
    endpoint: subscriptionJson.endpoint,
    toJSON: () => subscriptionJson,
    getKey: () => null,
    unsubscribe: async () => true,
    options: { userVisibleOnly: true, applicationServerKey: null },
  };
}

function stubBrowser(options: BrowserOptions = {}) {
  const {
    permission = "default",
    answer = "granted",
    unsupported = false,
    subscribed = false,
    iosNotInstalled = false,
  } = options;
  let current = permission;
  requestPermission.mockImplementation(async () => {
    current = answer;
    return answer;
  });
  subscribe.mockImplementation(async () => fakeSubscription());
  getSubscription.mockImplementation(async () =>
    subscribed ? fakeSubscription() : null,
  );
  const registration = {
    scope: "/",
    pushManager: { subscribe, getSubscription },
    showNotification: vi.fn(),
  };
  register.mockImplementation(async () => registration);

  vi.stubGlobal(
    "matchMedia",
    (query: string) =>
      ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false,
      }) as MediaQueryList,
  );

  if (iosNotInstalled) {
    Object.defineProperty(window.navigator, "userAgent", {
      configurable: true,
      value:
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
    });
    Object.defineProperty(window.navigator, "platform", {
      configurable: true,
      value: "iPhone",
    });
    Object.defineProperty(window.navigator, "maxTouchPoints", {
      configurable: true,
      value: 5,
    });
    Object.defineProperty(window.navigator, "standalone", {
      configurable: true,
      value: false,
    });
  }

  if (unsupported) {
    Reflect.deleteProperty(window, "Notification");
    Reflect.deleteProperty(window, "PushManager");
    Reflect.deleteProperty(window.navigator, "serviceWorker");
    return;
  }

  class FakeNotification {
    static get permission(): NotificationPermission {
      return current;
    }

    static requestPermission = requestPermission;
  }
  vi.stubGlobal("Notification", FakeNotification);
  vi.stubGlobal("PushManager", class {});
  Object.defineProperty(window.navigator, "serviceWorker", {
    configurable: true,
    value: {
      register,
      ready: Promise.resolve(registration),
      getRegistration: async () => registration,
      getRegistrations: async () => [registration],
      controller: null,
      addEventListener: () => undefined,
      removeEventListener: () => undefined,
    },
  });
}

const posts = () =>
  calls.filter(
    (call) =>
      call.method === "POST" && call.path === "/api/v1/me/push-subscriptions",
  );

const puts = () =>
  calls.filter(
    (call) =>
      call.method === "PUT" &&
      call.path === "/api/v1/me/notification-preferences",
  );

const navigatorDefaults = Object.getOwnPropertyDescriptors(window.navigator);

beforeEach(() => {
  calls.length = 0;
  requestPermission.mockReset();
  subscribe.mockReset();
  getSubscription.mockReset();
  register.mockReset();
  const Native = globalThis.Request;
  vi.stubGlobal(
    "Request",
    class extends Native {
      constructor(input: RequestInfo | URL, init?: RequestInit) {
        super(
          typeof input === "string"
            ? new URL(input, window.location.href)
            : input,
          init,
        );
      }
    },
  );
});

afterEach(() => {
  vi.restoreAllMocks();
  vi.unstubAllGlobals();
  vi.resetModules();
  for (const name of [
    "userAgent",
    "platform",
    "maxTouchPoints",
    "standalone",
    "serviceWorker",
    "permissions",
  ]) {
    const original = navigatorDefaults[name];
    if (original) {
      Object.defineProperty(window.navigator, name, original);
    } else {
      Reflect.deleteProperty(window.navigator, name);
    }
  }
});

async function renderPanel(
  preferences: NotificationPreferences,
  holdSubscriptionPost?: Promise<void>,
) {
  stubApi(preferences, holdSubscriptionPost);
  const { NoticesPanel } =
    await import("@/components/notifications/NoticesPanel");
  return render(<NoticesPanel initial={preferences} />);
}

/** O texto aparece numa região anunciada (`aria-live` ou `role=status/alert`). */
async function expectAnnounced(text: string | RegExp) {
  await waitFor(() => {
    const live = [
      ...document.querySelectorAll(
        '[aria-live="polite"], [aria-live="assertive"], [role="status"], [role="alert"]',
      ),
    ];
    const matches = live.some((element) =>
      typeof text === "string"
        ? (element.textContent ?? "").includes(text)
        : text.test(element.textContent ?? ""),
    );
    expect(
      matches,
      `"${String(text)}" não está numa região aria-live; regiões: ${live.map((element) => element.textContent).join(" | ")}`,
    ).toBe(true);
  });
}

function bytesOf(key: unknown): number[] {
  if (typeof key === "string") {
    const base64 = key.replaceAll("-", "+").replaceAll("_", "/");
    return [...atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "="))].map(
      (char) => char.charCodeAt(0),
    );
  }
  if (key instanceof ArrayBuffer) {
    return [...new Uint8Array(key)];
  }
  if (ArrayBuffer.isView(key)) {
    return [...new Uint8Array(key.buffer, key.byteOffset, key.byteLength)];
  }
  return [];
}

describe("NoticesPanel — Avisos", () => {
  it("SPEC-0030:UT-03 com a permissão em default mostra Ativar avisos e não pede permissão antes do toque", async () => {
    stubBrowser({ permission: "default" });

    await renderPanel(initialPreferences);

    expect(
      await screen.findByRole("button", { name: "Ativar avisos" }),
    ).toBeTruthy();
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(
      requestPermission,
      "pediu permissão sem o toque",
    ).not.toHaveBeenCalled();
    expect(subscribe, "inscreveu sem o toque").not.toHaveBeenCalled();
    expect(posts(), "gravou inscrição sem o toque").toHaveLength(0);
  });

  it("SPEC-0030:UT-03 depois do toque e da permissão concedida, inscreve com a chave pública, grava e anuncia os avisos ativos", async () => {
    stubBrowser({ permission: "default", answer: "granted" });
    await renderPanel(initialPreferences);

    fireEvent.click(
      await screen.findByRole("button", { name: "Ativar avisos" }),
    );

    await expectAnnounced("Avisos ativos neste aparelho");
    expect(requestPermission).toHaveBeenCalledTimes(1);
    expect(subscribe).toHaveBeenCalledTimes(1);
    const options = subscribe.mock.calls[0]?.[0];
    expect(options?.userVisibleOnly).toBe(true);
    expect(
      bytesOf(options?.applicationServerKey),
      "applicationServerKey não é a chave de GET /api/v1/push/public-key",
    ).toEqual(bytesOf(publicKey));
    expect(
      calls.some((call) => call.path === "/api/v1/push/public-key"),
      "a chave pública não veio da API",
    ).toBe(true);

    await waitFor(() => expect(posts()).toHaveLength(1));
    const post = posts()[0]!;
    expect(post.headers.get("X-CSRF")).toBe("csrf-token");
    expect(JSON.parse(post.body)).toEqual(subscriptionJson);
  });

  it("SPEC-0030:UT-03 com a permissão concedida e o aparelho inscrito mostra a situação e os quatro interruptores", async () => {
    stubBrowser({ permission: "granted", subscribed: true });

    await renderPanel(activePreferences);

    await expectAnnounced("Avisos ativos neste aparelho");
    const expected = [
      activePreferences.newShowPush,
      activePreferences.dailyDigestEmail,
      activePreferences.reminderPush,
      activePreferences.reminderEmail,
    ];
    for (const [index, name] of switchNames.entries()) {
      const control = await screen.findByRole("switch", { name });
      expect(control.getAttribute("aria-checked"), name).toBe(
        String(expected[index]),
      );
    }
    expect(screen.queryByRole("button", { name: "Ativar avisos" })).toBeNull();
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it("SPEC-0030:UT-03 ligar Resumo diário por e-mail salva as quatro preferências com X-CSRF e marca o interruptor", async () => {
    stubBrowser({ permission: "granted", subscribed: true });
    await renderPanel(activePreferences);

    const digest = await screen.findByRole("switch", {
      name: "Resumo diário por e-mail",
    });
    expect(digest.getAttribute("aria-checked")).toBe("false");
    fireEvent.click(digest);

    await waitFor(() => expect(puts()).toHaveLength(1));
    const put = puts()[0]!;
    expect(put.headers.get("X-CSRF")).toBe("csrf-token");
    expect(JSON.parse(put.body)).toEqual({
      newShowPush: true,
      dailyDigestEmail: true,
      reminderPush: true,
      reminderEmail: false,
    });
    await waitFor(() =>
      expect(
        screen
          .getByRole("switch", { name: "Resumo diário por e-mail" })
          .getAttribute("aria-checked"),
      ).toBe("true"),
    );
  });

  it("SPEC-0030:UT-03 com a permissão negada no navegador mostra que os avisos estão bloqueados e nada é gravado", async () => {
    // Emenda da revisão G4: com a permissão já negada, o bloqueio aparece ao abrir, sem o botão (ver UT-05).
    stubBrowser({ permission: "denied" });
    await renderPanel(initialPreferences);

    await expectAnnounced("Os avisos estão bloqueados no navegador.");
    expect(subscribe).not.toHaveBeenCalled();
    expect(posts()).toHaveLength(0);
  });

  it("SPEC-0030:UT-03 permissão recusada no pedido mostra que os avisos estão bloqueados e nada é gravado", async () => {
    stubBrowser({ permission: "default", answer: "denied" });
    await renderPanel(initialPreferences);

    fireEvent.click(
      await screen.findByRole("button", { name: "Ativar avisos" }),
    );

    await expectAnnounced("Os avisos estão bloqueados no navegador.");
    expect(requestPermission).toHaveBeenCalledTimes(1);
    expect(subscribe).not.toHaveBeenCalled();
    expect(posts()).toHaveLength(0);
  });

  it("SPEC-0030:UT-03 sem suporte a push não mostra o botão, diz que o navegador não recebe avisos e nada é gravado", async () => {
    stubBrowser({ unsupported: true });

    await renderPanel(initialPreferences);

    await expectAnnounced("Este navegador não recebe avisos.");
    expect(screen.queryByRole("button", { name: "Ativar avisos" })).toBeNull();
    expect(posts()).toHaveLength(0);
  });

  it("SPEC-0030:UT-03 no iPhone sem instalar mostra a instrução da tela de início e não pede permissão", async () => {
    stubBrowser({ permission: "default", iosNotInstalled: true });

    await renderPanel(initialPreferences);

    expect(
      await screen.findByText(
        "Adicione o Subsolo à tela de início para receber avisos",
        { exact: false },
      ),
    ).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Ativar avisos" })).toBeNull();
    expect(requestPermission).not.toHaveBeenCalled();
    expect(posts()).toHaveLength(0);
  });
});

/** Uma promessa que o teste resolve quando quiser. */
function deferred<T = void>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => {
    resolve = done;
  });
  return { promise, resolve };
}

/** O texto está em algum lugar da tela. */
function shown(text: string): boolean {
  return (document.body.textContent ?? "").includes(text);
}

describe("NoticesPanel — Avisos fora de sincronia", () => {
  it("SPEC-0030:UT-05 com uma inscrição no navegador, ao abrir registra de novo no servidor antes de mostrar os avisos ativos", async () => {
    stubBrowser({ permission: "granted", subscribed: true });
    const post = deferred();

    await renderPanel(activePreferences, post.promise);

    await waitFor(
      () =>
        expect(
          posts(),
          "a inscrição do navegador não foi registrada de novo ao abrir",
        ).toHaveLength(1),
      { timeout: 2000 },
    );
    const saved = posts()[0]!;
    expect(saved.headers.get("X-CSRF")).toBe("csrf-token");
    expect(JSON.parse(saved.body)).toEqual(subscriptionJson);
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(
      shown("Avisos ativos neste aparelho"),
      "mostrou os avisos ativos antes de o servidor confirmar a inscrição",
    ).toBe(false);

    post.resolve();

    await expectAnnounced("Avisos ativos neste aparelho");
    expect(posts(), "registrou a inscrição mais de uma vez").toHaveLength(1);
    expect(subscribe, "inscreveu de novo no navegador").not.toHaveBeenCalled();
    expect(requestPermission).not.toHaveBeenCalled();
  });

  it("SPEC-0030:UT-05 com a permissão já negada (Notification.permission) mostra o bloqueio ao abrir, sem o botão", async () => {
    stubBrowser({ permission: "denied" });

    await renderPanel(initialPreferences);

    await expectAnnounced("Os avisos estão bloqueados no navegador.");
    expect(screen.queryByRole("button", { name: "Ativar avisos" })).toBeNull();
    expect(requestPermission).not.toHaveBeenCalled();
    expect(posts()).toHaveLength(0);
  });

  it("SPEC-0030:UT-05 com a permissão já negada (Permissions API) mostra o bloqueio ao abrir, sem o botão", async () => {
    // O Chromium sem interface responde `default` no `Notification.permission`; a Permissions API diz `denied`.
    stubBrowser({ permission: "default" });
    Object.defineProperty(window.navigator, "permissions", {
      configurable: true,
      value: {
        query: async () => ({ state: "denied" }) as PermissionStatus,
      },
    });

    await renderPanel(initialPreferences);

    await expectAnnounced("Os avisos estão bloqueados no navegador.");
    expect(screen.queryByRole("button", { name: "Ativar avisos" })).toBeNull();
    expect(requestPermission).not.toHaveBeenCalled();
    expect(posts()).toHaveLength(0);
  });

  it("SPEC-0030:UT-05 durante a ativação mostra Ativando… e o botão fica desabilitado", async () => {
    stubBrowser({ permission: "granted" });
    const subscribed = deferred();
    subscribe.mockImplementation(async () => {
      await subscribed.promise;
      return fakeSubscription();
    });
    await renderPanel(initialPreferences);

    fireEvent.click(
      await screen.findByRole("button", { name: "Ativar avisos" }),
    );

    await waitFor(() => expect(subscribe).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(
        shown("Ativando…"),
        "durante a ativação não aparece o texto activating (Ativando…)",
      ).toBe(true),
    );
    const button = screen.getByRole("button");
    expect(
      button.hasAttribute("disabled") ||
        button.getAttribute("aria-disabled") === "true",
      "o botão não está desabilitado durante a ativação",
    ).toBe(true);
    fireEvent.click(button);
    expect(
      subscribe,
      "o segundo toque inscreveu de novo",
    ).toHaveBeenCalledTimes(1);

    subscribed.resolve();

    await expectAnnounced("Avisos ativos neste aparelho");
    expect(shown("Ativando…")).toBe(false);
  });
});

/**
 * SPEC-0031 H30-03 — com os quatro campos obrigatórios no `PUT /api/v1/me/notification-preferences` (400
 * `notifications.invalid_preferences` sem algum), cada interruptor do painel "Avisos" continua mandando os quatro
 * booleanos, com só o tocado invertido.
 */
describe("NoticesPanel — os quatro campos no PUT", () => {
  it.each(switchNames)(
    "SPEC-0031:H30-03 tocar em %s manda os quatro campos booleanos",
    async (name) => {
      stubBrowser({ permission: "granted", subscribed: true });
      await renderPanel(activePreferences);

      fireEvent.click(await screen.findByRole("switch", { name }));

      await waitFor(() => expect(puts()).toHaveLength(1));
      const body = JSON.parse(puts()[0]!.body) as Record<string, unknown>;
      expect(Object.keys(body).sort()).toEqual([
        "dailyDigestEmail",
        "newShowPush",
        "reminderEmail",
        "reminderPush",
      ]);
      for (const value of Object.values(body)) {
        expect(typeof value).toBe("boolean");
      }
    },
  );
});
