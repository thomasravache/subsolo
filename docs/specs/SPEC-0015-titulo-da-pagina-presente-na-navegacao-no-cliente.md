---
id: SPEC-0015
title: Título da página presente na navegação no cliente
tier: lite
type: fix
user_facing: true
status: approved
created: 2026-10-17
parent: SPEC-0002
depends_on: []
consumes_contract: []
touches: [web/next.config.ts, web/src/next-config.test.ts, e2e/tests/agenda/**, e2e/tests/design/**]
adrs: [ADR-0013]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0015 — Título da página presente na navegação no cliente

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Na navegação no cliente (clique num `next/link`), o Next 16 entrega os metadados da página nova num bloco separado do conteúdo, e o documento fica cerca de 300 ms sem `<title>`. Nesse intervalo, o leitor de tela anuncia uma página sem nome, o histórico do navegador grava a entrada sem título e o axe acusa `document-title` (WCAG 2.4.2). O problema foi achado na SPEC-0011 (Emenda 3): lá, a troca de cidade passou a recarregar a página inteira. Mas o MVP vai ter muitos links internos com título dinâmico (card do show, perfis, navegação inferior).

**Esperado:** em toda navegação no cliente, o documento sempre tem um `<title>` não vazio, e o título da página nova aparece assim que a URL muda.

## 2. Causa Raiz
O Next 16 faz *streaming* dos metadados para quem não é robô: o `<title>` sai depois do conteúdo, num bloco à parte. Robôs de busca e de prévia de link recebem os metadados bloqueantes, conforme a lista `htmlLimitedBots`. O `web/next.config.ts` não configura `htmlLimitedBots`, então vale o padrão, que trata navegadores comuns como não robôs.

## 3. Mudança Proposta
- `htmlLimitedBots: /.*/` no `web/next.config.ts`: os metadados passam a ser bloqueantes para todos os agentes (decisão do Thomas, 2026-10-17, no épico SPEC-0002).
- Se o E2E-01 mostrar que isso corrige o HTML inicial, mas não a navegação no cliente, o Implementer reporta `SPEC_DEFECT`. A spec ganha uma emenda trocando a correção por navegação completa (`<a>` simples) nos links do shell, como já fizemos no `CitySwitcher`. Não há desvio silencioso.
- **Não muda:** textos, status HTTP (ADR-0016), CSP (ADR-0017), o `CitySwitcher` (continua com `window.location.assign`).

**Padrão seguido:** `web/next.config.ts` existente (SPEC-0008/0011); E2E com axe de `e2e/tests/agenda/**` e `e2e/tests/design/**` (SPEC-0011).

**Rollback:** revert do commit.

**Outras ocorrências:** nenhuma hoje. Toda spec do MVP com link interno herda a proteção; o E2E-01 serve de guarda.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o defeito é o comportamento atual; o E2E-01 reproduz e falha antes da correção.
- **UT-01** — Dado o `web/next.config.ts`, quando importado em `web/src/next-config.test.ts`, então `htmlLimitedBots` casa com um user agent comum de Chrome mobile e de Safari iOS.
- **E2E-01** — Dada a aplicação completa, nos projetos mobile-chrome e mobile-safari, um `MutationObserver` instalado antes do clique registra todo valor de `document.title`. Quando o fã sai de `/cidade-exemplo` por um `next/link` do shell para outra página com título próprio, então nenhum valor registrado é vazio, o título final é o da página nova, e o axe (`document-title`) roda logo depois da troca de URL sem violação. Falha antes da correção. [jornada: agenda]

## 5. Questões em Aberto
- [x] Qual solução adotar? — Metadados bloqueantes com `htmlLimitedBots` global, provados por teste; se não resolverem a navegação no cliente, navegação completa nos links afetados (Thomas, 2026-10-17)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 93aac2d (árvore suja) | 2026-10-17 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
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
