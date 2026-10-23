/**
 * SPEC-0030 — o que o navegador oferece para os avisos (ADR-0013): iPhone/iPad fora do app instalado (o Safari só
 * entrega push ao Subsolo aberto da tela de início), sem suporte (sem `Notification`, `PushManager` ou service worker)
 * ou com suporte.
 */
export type PushSupport = "ios-install" | "unsupported" | "supported";

/** O caminho do service worker do PWA (`web/public/sw.js`). */
export const serviceWorkerPath = "/sw.js";

function isIos(): boolean {
  const { userAgent, platform, maxTouchPoints } = window.navigator;
  return (
    /iPhone|iPad|iPod/.test(userAgent) ||
    /iPhone|iPad|iPod/.test(platform) ||
    (platform === "MacIntel" && maxTouchPoints > 1)
  );
}

function isStandalone(): boolean {
  const standalone = (window.navigator as { standalone?: boolean }).standalone;
  return (
    standalone === true ||
    window.matchMedia?.("(display-mode: standalone)").matches === true
  );
}

export function pushSupport(): PushSupport {
  if (isIos() && !isStandalone()) {
    return "ios-install";
  }
  return "Notification" in window &&
    "PushManager" in window &&
    "serviceWorker" in window.navigator
    ? "supported"
    : "unsupported";
}

/** Chave base64url (sem preenchimento) em bytes, para o `applicationServerKey`. */
export function keyBytes(key: string): Uint8Array<ArrayBuffer> {
  const base64 = key.replaceAll("-", "+").replaceAll("_", "/");
  const binary = atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, "="));
  const bytes = new Uint8Array(new ArrayBuffer(binary.length));
  for (let index = 0; index < binary.length; index++) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}

/** O registro do service worker do PWA (registra se ainda não está). */
export function registration(): Promise<ServiceWorkerRegistration> {
  return window.navigator.serviceWorker.register(serviceWorkerPath);
}
