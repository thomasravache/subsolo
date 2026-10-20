/**
 * SPEC-0028 — BR Code do Pix estático ("copia e cola", sem valor), montado no servidor do web a partir da configuração
 * (`Support:PixKey`, `Support:PixName`, `Support:PixCity`). Scaffold do contrato: a implementação entra no Green.
 */
export interface PixReceiver {
  key: string;
  name: string;
  city: string;
}

/** CRC16-CCITT (polinômio 0x1021, início 0xFFFF) de `text`, em 4 dígitos hexadecimais maiúsculos. */
export function crc16(text: string): string {
  throw new Error(`SPEC-0028: crc16 ainda não implementado (${text.length})`);
}

/** O payload EMV do Pix (campos 00, 26, 52, 53, 58, 59, 60, 62 e 63) para `receiver`. */
export function pixPayload(receiver: PixReceiver): string {
  throw new Error(
    `SPEC-0028: pixPayload ainda não implementado (${receiver.key.length})`,
  );
}
