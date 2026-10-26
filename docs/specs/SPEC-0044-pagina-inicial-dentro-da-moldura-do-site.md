---
id: SPEC-0044
title: Página inicial dentro da moldura do site
tier: lite
type: fix
user_facing: true
status: in-progress
created: 2026-10-26
parent:
depends_on: []
consumes_contract: []
touches: [web/src/app/page.tsx, "web/src/app/(public)/page.tsx", e2e/tests/home/**]
adrs: []
external: []
size: S
approved_by: thomas
approved_at: 2026-10-26
---

# SPEC-0044 — Página inicial dentro da moldura do site

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
A página inicial (`/`) mostra só "Subsolo — Agenda de shows de metal underground por cidade. Em construção." e não leva a lugar nenhum. Faltam o cabeçalho com o seletor de cidade, o "Entrar", o rodapé (com "Apoie") e a navegação inferior do celular. Quem chega pelo endereço do site não acha a agenda. O Thomas encontrou isso em 2026-10-26, abrindo o site pelo túnel: "eu abri essa tela e não consigo ir pra nenhum lugar".

- Esperado: `/` usa a mesma moldura de todas as páginas públicas, então dá para escolher a cidade, entrar e chegar ao rodapé a partir dela.

## 2. Causa Raiz
`web/src/app/page.tsx` fica fora do grupo de rotas `(public)`, cujo `layout.tsx` monta o `AppShell` (SPEC-0011) com as cidades da API. A página inicial nasceu no esqueleto da fundação, antes do shell, e não foi movida quando o shell chegou.

## 3. Mudança Proposta
- Mover `web/src/app/page.tsx` para `web/src/app/(public)/page.tsx`. A URL continua `/`, e a página passa a herdar o `AppShell`: cabeçalho com seletor de cidade, rodapé com "Apoie" e "Privacidade", e a navegação inferior no celular, cuja aba "Conta" leva o anônimo a `/entrar` (no desktop, o "Entrar" fica no cabeçalho).
- O conteúdo do meio não muda: título, frase e o aviso "Sua conta foi excluída." da SPEC-0019 em `/?conta=excluida`.
- Telas: a moldura é a do Stitch já aprovada na SPEC-0011 ("Agenda Curitiba - Subsolo" e "Agenda Curitiba - Desktop"). Não há tela nova. Uma página inicial própria, como "Escolha sua cidade", fica para outra spec, com desenho no Stitch.
- O que NÃO muda: a API, as outras rotas, o `AppShell` e os textos.

**Padrão seguido:** as páginas de `web/src/app/(public)/` (por exemplo `apoie/page.tsx`), que recebem a moldura pelo `(public)/layout.tsx`.

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma. As outras páginas fora de `(public)` são do admin (`/admin`, moldura própria da SPEC-0017) e os arquivos do Next (`manifest`, `icon`).

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o aviso de conta excluída em `/?conta=excluida` já é provado pela E2E da SPEC-0019, que continua valendo sem mudança.
- **E2E-01** — Dado um visitante anônimo no celular (mobile-chrome), quando abre `/`, então vê o título "Subsolo", o seletor de cidade no cabeçalho, a aba "Conta" da navegação inferior levando a `/entrar` e o rodapé com "Apoie". Escolhendo uma cidade no seletor, chega em `/{cidade}` com a agenda. A página não tem violações do axe. Hoje falha porque `/` não tem cabeçalho nem rodapé. [jornada: agenda]
- Unitário: N/A — a mudança é só de lugar no roteamento do Next, sem lógica nova. O que importa é a página montada com a moldura, e isso só a E2E prova.

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [ ] Red: E2E-01 com a tag `SPEC-0044:E2E-01`, falhando porque `/` não tem cabeçalho nem rodapé
- [ ] Green: `web/src/app/page.tsx` movida para `web/src/app/(public)/page.tsx`
- [ ] Validar: build + suíte + arquitetura (G2/G3) e E2E-01 passando
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 16d10ef | 2026-10-26 |
| G1 Red | PASS | MANUAL: verify (worktree): red antes do green, primeiro red b5b2be4 (E2E-01 falhou: sem banner na /); E2E fora do comando test do sdd-config; green E2E-01 em c07e65d (Emenda v2) | 2026-10-26 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — c07e65d | 2026-10-26 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 694ms) — c07e65d | 2026-10-26 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem blocker/major; H44-01 Emenda v2 aprovada pelo Thomas em 2026-10-26; H44-02 e H44-03 nits não aplicados) — c07e65d | 2026-10-26 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v2 | 2026-10-26 | E2E-01 e §3: no celular, a entrada é a aba "Conta" da navegação inferior (→ `/entrar`), não um link "Entrar"; o rodapé tem "Apoie" e "Privacidade", sem "Termos" | O "Entrar" do cabeçalho só aparece no desktop (`Header.tsx`, SPEC-0016/0027); o E2E-01 falhou procurando o link no celular. A intenção do cenário (chegar ao login a partir de `/`) não muda | SPEC-0044 | Aprovado (Thomas, 2026-10-26) |
