import type { PixReceiver } from "./pix";

/**
 * SPEC-0028 — configuração das páginas institucionais, lida só no servidor do web (variáveis de ambiente sem o prefixo
 * público do Next.js: nunca vão para o navegador). Definidas no go-live (SPEC-0003); a pilha de teste usa valores de
 * teste (`tools/compose-up.sh`).
 *
 * - `Support__PixKey`, `Support__PixName`, `Support__PixCity` (`Support:PixKey`…): o Pix da página Apoie;
 * - `Site__ContactEmail` (`Site:ContactEmail`): o canal do titular dos dados (privacidade e termos).
 */

/** Nome e cidade do recebedor quando a chave existe mas eles não foram configurados. */
const defaultPixName = "Subsolo";
const defaultPixCity = "Curitiba";

function setting(name: string): string {
  return (process.env[name] ?? "").trim();
}

/** O recebedor do Pix, ou `undefined` sem chave configurada (a página mostra "chega em breve"). */
export function pixReceiver(): PixReceiver | undefined {
  const key = setting("Support__PixKey");
  if (!key) {
    return undefined;
  }
  return {
    key,
    name: setting("Support__PixName") || defaultPixName,
    city: setting("Support__PixCity") || defaultPixCity,
  };
}

/** O e-mail de contato do titular, ou `undefined` sem configuração. */
export function contactEmail(): string | undefined {
  return setting("Site__ContactEmail") || undefined;
}
