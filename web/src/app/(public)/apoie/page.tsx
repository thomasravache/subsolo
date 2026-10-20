import type { Metadata } from "next";

import { getSupporters } from "@/api/supporters";
import { pixReceiver } from "@/components/site/config";
import { CopyPixKey } from "@/components/site/CopyPixKey";
import { supportMessages } from "@/components/site/messages";
import { PixQrCode, pixQrCodeSource } from "@/components/site/PixQrCode";
import { SupportersList } from "@/components/site/SupportersList";
import { mainContentId } from "@/components/ui/shell";

/**
 * SPEC-0028 — `/apoie` (tela "Apoie o Subsolo v2" do Stitch, com o QR code gerado da chave real e os textos do §6):
 * texto, o Pix (QR code gerado no servidor a partir de `Support:PixKey`, a chave em texto e "Copiar chave"; sem chave,
 * "O Pix do Subsolo chega em breve.") e a lista de apoiadores. Renderizada por requisição; com a API fora do ar a página
 * sai com o Pix e o aviso da lista.
 */
export const metadata: Metadata = { title: "Apoie o Subsolo | Subsolo" };

export const instant = false;

export default async function ApoiePage() {
  const receiver = pixReceiver();
  const [qrCode, supporters] = await Promise.all([
    receiver ? pixQrCodeSource(receiver) : undefined,
    getSupporters(),
  ]);

  return (
    <main
      id={mainContentId}
      className="mx-auto flex w-full max-w-2xl flex-1 flex-col gap-10 px-4 py-10"
    >
      <div className="flex flex-col gap-4">
        <h1 className="font-display text-5xl uppercase leading-none tracking-wide text-text">
          Apoie o Subsolo
        </h1>
        <p className="text-lg text-text-muted">
          O Subsolo é feito pela cena e não tem anúncios. Se ele te ajuda a
          achar shows, apoie com um Pix de qualquer valor.
        </p>
      </div>

      <section
        aria-labelledby="chave-pix"
        className="flex flex-col items-center gap-6 rounded-md border border-border bg-surface p-6"
      >
        <div className="flex w-full flex-col gap-1">
          <p className="font-label text-xs font-bold uppercase tracking-wider text-text-muted">
            Qualquer valor
          </p>
          <h2
            id="chave-pix"
            className="font-display text-3xl uppercase tracking-wide text-text"
          >
            Chave Pix
          </h2>
        </div>
        {receiver && qrCode ? (
          <>
            <PixQrCode source={qrCode} />
            <CopyPixKey pixKey={receiver.key} />
          </>
        ) : (
          <p className="w-full text-base text-text-muted">
            {supportMessages.pixSoon}
          </p>
        )}
      </section>

      <SupportersList result={supporters} />
    </main>
  );
}
