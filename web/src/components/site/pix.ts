/**
 * SPEC-0028 — BR Code do Pix estático ("copia e cola", sem valor), montado no servidor do web a partir da configuração
 * (`Support:PixKey`, `Support:PixName`, `Support:PixCity`). Formato EMV do manual do BR Code do Banco Central: cada
 * campo é `id` (2 dígitos) + tamanho (2 dígitos) + valor, e o CRC16 fecha o payload.
 */
export interface PixReceiver {
  key: string;
  name: string;
  city: string;
}

/** Tamanhos máximos do manual do BR Code: nome do recebedor (campo 59) e cidade (campo 60). */
const maxNameLength = 25;
const maxCityLength = 15;

/** Um campo TLV: id, tamanho em 2 dígitos e o valor. */
function field(id: string, value: string): string {
  return `${id}${value.length.toString().padStart(2, "0")}${value}`;
}

/** CRC16-CCITT (polinômio 0x1021, início 0xFFFF) de `text`, em 4 dígitos hexadecimais maiúsculos. */
export function crc16(text: string): string {
  let crc = 0xffff;
  for (const byte of new TextEncoder().encode(text)) {
    crc ^= byte << 8;
    for (let bit = 0; bit < 8; bit++) {
      crc = crc & 0x8000 ? ((crc << 1) ^ 0x1021) & 0xffff : (crc << 1) & 0xffff;
    }
  }
  return crc.toString(16).toUpperCase().padStart(4, "0");
}

/** Nome e cidade do recebedor: sem acentos (o manual pede caracteres ASCII) e no tamanho máximo do campo. */
function plain(text: string, max: number): string {
  return text
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .replace(/[^\x20-\x7e]/g, "")
    .trim()
    .slice(0, max);
}

/** O payload EMV do Pix (campos 00, 26, 52, 53, 58, 59, 60, 62 e 63) para `receiver`. */
export function pixPayload(receiver: PixReceiver): string {
  const account = field("00", "br.gov.bcb.pix") + field("01", receiver.key);
  const body =
    field("00", "01") +
    field("26", account) +
    field("52", "0000") +
    field("53", "986") +
    field("58", "BR") +
    field("59", plain(receiver.name, maxNameLength)) +
    field("60", plain(receiver.city, maxCityLength)) +
    field("62", field("05", "***")) +
    "6304";
  return body + crc16(body);
}
