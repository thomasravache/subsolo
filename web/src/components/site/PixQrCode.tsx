import QRCode from "qrcode";

import { pixPayload, type PixReceiver } from "./pix";

/** Texto alternativo do QR code (§6). */
export const pixQrCodeName = "QR code do Pix do Subsolo";

/**
 * SPEC-0028 — o QR code do BR Code do Pix de `receiver`, gerado no servidor como SVG (preto no branco, com a margem que
 * os leitores dos bancos pedem), num endereço `data:` para a imagem: a CSP permite `img-src data:`, e nada roda no
 * navegador nem vem de endereço externo.
 */
export async function pixQrCodeSource(receiver: PixReceiver): Promise<string> {
  const svg = await QRCode.toString(pixPayload(receiver), {
    type: "svg",
    errorCorrectionLevel: "M",
    margin: 2,
    color: { dark: "#000000", light: "#ffffff" },
  });
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString("base64")}`;
}

/** SPEC-0028 — a imagem do QR code (gerado por {@link pixQrCodeSource}). */
export function PixQrCode({ source }: { source: string }) {
  return (
    // eslint-disable-next-line @next/next/no-img-element -- imagem `data:` gerada no servidor; não passa pelo otimizador.
    <img
      src={source}
      alt={pixQrCodeName}
      width={224}
      height={224}
      className="size-56 rounded-md bg-white"
    />
  );
}
