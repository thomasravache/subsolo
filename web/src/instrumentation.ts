import { registerOTel } from "@vercel/otel";

/**
 * SPEC-0009 — OpenTelemetry do Next.js (ADR-0010): os spans do web (requisições, renderização e as chamadas à API)
 * vão por OTLP (`OTEL_EXPORTER_OTLP_ENDPOINT`, `http/protobuf` por padrão) para o mesmo destino da API — o Seq, ligado
 * pelo AppHost. O `traceparent` recebido continua o trace (propagador W3C), e as chamadas à API (`API_URL`) levam o
 * contexto adiante: um único trace navegador → web → API → PostgreSQL. Sem destino configurado, nada é registrado.
 */
export function register() {
  if (!process.env.OTEL_EXPORTER_OTLP_ENDPOINT) {
    return;
  }

  const apiUrl = process.env.API_URL;
  registerOTel({
    serviceName: process.env.OTEL_SERVICE_NAME ?? "web",
    propagators: ["tracecontext", "baggage"],
    instrumentationConfig: {
      fetch: { propagateContextUrls: apiUrl ? [apiUrl] : [] },
    },
  });
}
