// @vitest-environment node
import { describe, expect, it } from "vitest";

import { crc16, pixPayload } from "@/components/site/pix";

/**
 * SPEC-0028 UT-01 (BR Code) — o "copia e cola" do Pix estático, sem valor, montado no servidor do web a partir da
 * configuração (chave, nome e cidade do recebedor), segue o formato EMV do Pix: campos 00, 26 (GUI `br.gov.bcb.pix` +
 * chave), 52, 53=986, 58=BR, 59, 60, 62 e 63, nessa ordem, sem o 54 (valor), com o CRC16-CCITT (polinômio 0x1021,
 * início 0xFFFF) em 4 dígitos hexadecimais maiúsculos.
 *
 * Payloads de referência:
 * - o exemplo do manual do BR Code do Banco Central (chave aleatória `123e4567-…`, "Fulano de Tal", "BRASILIA"), com o
 *   CRC `1D3D` publicado no manual;
 * - a chave de exemplo da spec (`exemplo@subsolo.test`, "Subsolo", "Curitiba"), montada e conferida fora do código.
 */
const bcbExample =
  "00020126580014br.gov.bcb.pix0136123e4567-e12b-12d1-a456-4266554400005204000053039865802BR5913Fulano de Tal6008BRASILIA62070503***63041D3D";

const specExample =
  "00020126420014br.gov.bcb.pix0120exemplo@subsolo.test5204000053039865802BR5907Subsolo6008Curitiba62070503***63041B1E";

type Field = { id: string; value: string };

/** Lê os campos TLV (id de 2 dígitos, tamanho de 2 dígitos, valor). */
function fields(payload: string): Field[] {
  const result: Field[] = [];
  let position = 0;
  while (position < payload.length) {
    const id = payload.slice(position, position + 2);
    const length = Number(payload.slice(position + 2, position + 4));
    expect(
      Number.isInteger(length),
      `tamanho inválido no campo ${id} (posição ${position}) de ${payload}`,
    ).toBe(true);
    const value = payload.slice(position + 4, position + 4 + length);
    expect(value.length, `campo ${id} cortado em ${payload}`).toBe(length);
    result.push({ id, value });
    position += 4 + length;
  }
  return result;
}

function valueOf(list: Field[], id: string): string | undefined {
  return list.find((field) => field.id === id)?.value;
}

describe("BR Code do Pix", () => {
  it("SPEC-0028:UT-01 o CRC16-CCITT confere com o exemplo do manual do Banco Central", () => {
    expect(crc16(bcbExample.slice(0, -4))).toBe("1D3D");
  });

  it("SPEC-0028:UT-01 monta exatamente o payload de referência do manual", () => {
    expect(
      pixPayload({
        key: "123e4567-e12b-12d1-a456-426655440000",
        name: "Fulano de Tal",
        city: "BRASILIA",
      }),
    ).toBe(bcbExample);
  });

  it("SPEC-0028:UT-01 com exemplo@subsolo.test, Subsolo e Curitiba segue o formato EMV, com CRC correto", () => {
    const payload = pixPayload({
      key: "exemplo@subsolo.test",
      name: "Subsolo",
      city: "Curitiba",
    });

    const list = fields(payload);
    expect(list.map((field) => field.id)).toEqual([
      "00",
      "26",
      "52",
      "53",
      "58",
      "59",
      "60",
      "62",
      "63",
    ]);
    expect(valueOf(list, "00")).toBe("01");
    const account = fields(valueOf(list, "26") ?? "");
    expect(valueOf(account, "00")).toBe("br.gov.bcb.pix");
    expect(valueOf(account, "01")).toBe("exemplo@subsolo.test");
    expect(valueOf(list, "52")).toBe("0000");
    expect(valueOf(list, "53")).toBe("986");
    expect(valueOf(list, "54"), "Pix de qualquer valor: sem o campo 54").toBe(
      undefined,
    );
    expect(valueOf(list, "58")).toBe("BR");
    expect(valueOf(list, "59")).toBe("Subsolo");
    expect(valueOf(list, "60")).toBe("Curitiba");
    expect(payload.slice(-8, -4)).toBe("6304");
    expect(valueOf(list, "63")).toMatch(/^[0-9A-F]{4}$/);
    expect(valueOf(list, "63")).toBe(crc16(payload.slice(0, -4)));
    expect(payload).toBe(specExample);
  });

  it("SPEC-0028:UT-01 o CRC muda com a chave (não é fixo)", () => {
    const one = pixPayload({
      key: "exemplo@subsolo.test",
      name: "Subsolo",
      city: "Curitiba",
    });
    const other = pixPayload({
      key: "outro@subsolo.test",
      name: "Subsolo",
      city: "Curitiba",
    });

    expect(one.slice(-4)).not.toBe(other.slice(-4));
    expect(other.slice(-4)).toBe(crc16(other.slice(0, -4)));
  });
});
