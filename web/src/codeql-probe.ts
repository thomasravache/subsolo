// SPEC-0013 item 8: amostra temporária para confirmar a supressão do CodeQL em TypeScript.
export function renderHash(): void {
  document.body.innerHTML = window.location.hash; // codeql[js/xss-through-dom] amostra da SPEC-0013 para confirmar a supressão
}
