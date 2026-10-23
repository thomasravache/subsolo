/*
 * SPEC-0030 — service worker do PWA (ADR-0013): só os avisos. Sem cache offline e sem eval (compatível com a CSP do
 * site, ADR-0017).
 * - push: mostra a notificação com o título, o corpo e o ícone do Subsolo; o payload é o JSON {title, body, url}.
 * - notificationclick: abre a URL do aviso (só do próprio site; fora dele, a página inicial), reaproveitando uma aba
 *   aberta com o mesmo endereço.
 */
const icon = "/icons/icon-192.png";

self.addEventListener("install", () => {
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(self.clients.claim());
});

function noticeOf(event) {
  try {
    const data = event.data ? event.data.json() : {};
    return {
      title:
        typeof data.title === "string" && data.title ? data.title : "Subsolo",
      body: typeof data.body === "string" ? data.body : "",
      url: typeof data.url === "string" ? data.url : "/",
    };
  } catch {
    return { title: "Subsolo", body: "", url: "/" };
  }
}

function sameOriginUrl(url) {
  try {
    const target = new URL(url, self.location.origin);
    return target.origin === self.location.origin
      ? target.href
      : self.location.origin + "/";
  } catch {
    return self.location.origin + "/";
  }
}

self.addEventListener("push", (event) => {
  const notice = noticeOf(event);
  event.waitUntil(
    self.registration.showNotification(notice.title, {
      body: notice.body,
      icon,
      badge: icon,
      lang: "pt-BR",
      data: { url: sameOriginUrl(notice.url) },
    }),
  );
});

self.addEventListener("notificationclick", (event) => {
  event.notification.close();
  const url = sameOriginUrl(
    event.notification.data && event.notification.data.url,
  );
  event.waitUntil(
    self.clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then((windows) => {
        const open = windows.find(
          (client) => client.url === url && "focus" in client,
        );
        return open ? open.focus() : self.clients.openWindow(url);
      }),
  );
});
