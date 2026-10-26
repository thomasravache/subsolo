---
id: SPEC-0042
title: E2E dos filtros da agenda independente do horário de domingo
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-10-26
parent:
depends_on: []
consumes_contract: []
touches: [e2e/tests/agenda/**]
adrs: []
external: []
size: S
approved_by: thomas
approved_at: 2026-10-26
---

# SPEC-0042 — E2E dos filtros da agenda independente do horário de domingo

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O E2E de filtros da SPEC-0025 ( da agenda, "Esta semana") falha quando roda no domingo depois das 23:00 de São Paulo. O PR #52 falhou assim: run 40256479624, as duas tentativas entre 23:30 e 23:59 de 2026-10-25 (domingo), com `POST /api/v1/shows: 400 … "StartsLocal": ["A data e a hora do show já passaram."]`.

- Reproduzir: rodar `(cd e2e && pnpm test tests/agenda/agenda-filtros.spec.ts)` num domingo depois das 23:00 (BRT).
- Esperado: o teste passa em qualquer dia e horário em que é possível ter um show futuro ainda dentro da semana. Quando isso é impossível (os últimos minutos de domingo), ele é pulado com o motivo escrito, em vez de falhar.
- Impacto: só no CI. Bloqueia PRs que rodam nessa janela e obriga a pedir nova execução. O produto não é afetado: a API está certa ao recusar show no passado.

## 2. Causa Raiz
Em `e2e/tests/agenda/agenda-filtros.spec.ts`, os dois shows "da semana" são cadastrados em `daysUntilSunday()` (0 no domingo) com horários fixos, 23:00 e 23:30. No domingo, esse é o dia de hoje. Passadas as 23:00, os horários já estão no passado e a API recusa o cadastro (regra da SPEC-0023). O teste presume que o domingo da semana ainda tem 23:00 e 23:30 no futuro.

## 3. Mudança Proposta
- Em `e2e/tests/agenda/filters.ts`, uma função pura `weekShowTimes(now: Date)` devolve os horários dos dois shows da semana, ou `null` quando não há margem para eles.
  - Se o domingo da semana é outro dia, os horários de hoje continuam (23:00 e 23:30).
  - No domingo antes das 22:30 de São Paulo, também continuam 23:00 e 23:30.
  - No domingo entre 22:30 e 23:39, ficam 23:58 e 23:59.
  - No domingo a partir das 23:40, devolve `null`. Não sobra margem para o teste, que leva até 5 min (o E2E roda com `retries: 0`; numa repetição o horário é recalculado).
- O teste de filtros da SPEC-0025 usa esses horários e chama `test.skip` com o motivo ("últimos 20 minutos de domingo: não há horário futuro dentro da semana") quando recebe `null`.
- O que NÃO muda: o produto (API, web), a regra "show no passado é recusado", os outros testes e as asserções do teste de filtros da SPEC-0025.

**Padrão seguido:** os helpers de data já existentes em `e2e/tests/agenda/filters.ts` (`daysUntilSunday`, `dayMonth`) e `localDate` em `e2e/tests/shows/journey.ts`, sempre no fuso `America/Sao_Paulo`. E `test.skip(condição, motivo)`, como os próprios testes do arquivo já fazem por projeto.

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma. Os outros E2E que cadastram show usam `localDate(n)` com n ≥ 1, ou seja, um dia futuro inteiro. Conferido com `grep localDate( e2e/tests`.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o teste de filtros "esta semana" da SPEC-0025 já fixa o comportamento atual e continua igual.
- **UT-01** — Dado `now` no domingo às 23:45 de São Paulo, quando `weekShowTimes(now)` é chamada, então devolve `null`. Dado domingo às 23:05, devolve 23:58 e 23:59, que estão no futuro. Dado domingo às 15:00 e uma quarta às 23:50, devolve 23:00 e 23:30. Teste Playwright sem navegador em `e2e/tests/agenda/`, rodando só no projeto mobile-chrome. Falha antes da correção porque a função não existe.
- E2E: N/A — a regressão de verdade depende do relógio de parede de domingo à noite e não dá para forçar na pilha do Compose (a API usa o relógio real). A UT-01 cobre a decisão, e o teste de filtros da SPEC-0025 continua rodando inteiro fora da janela.

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [x] Red: UT-01 com a tag `SPEC-0042:UT-01` em `e2e/tests/agenda/`, falhando porque `weekShowTimes` não existe
- [x] Green: `weekShowTimes(now)` em `e2e/tests/agenda/filters.ts` e o teste de filtros da SPEC-0025 usando os horários e o `test.skip` com motivo
- [x] Validar: `tsc --noEmit` do e2e, UT-01 e o teste de filtros da SPEC-0025 passando (G2); arquitetura (G3)
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 9c972fa | 2026-10-26 |
| G1 Red | PASS | MANUAL: verify G1: PASS (Red antes do Green, red só altera testes, rastreabilidade 1/1 UT-01); o comando `test` do sdd-config não roda o e2e, então o red foi rodado à parte: `cd e2e && BASE_URL=https://localhost:8443 pnpm exec playwright test tests/agenda/week-show-times.spec.ts --project mobile-chrome` exit 1 (1 failed: TypeError weekShowTimes is not a function) — ef7f487 | 2026-10-26 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — b5f2a86 | 2026-10-26 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 376ms) — b5f2a86 | 2026-10-26 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem blocker/major; nits H42-01 resolvido no texto da spec, H42-02 opcional não aplicado, H42-03 pré-existente fora do escopo) — b5f2a86 | 2026-10-26 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 18m 43s 903ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 10s 887ms) — 16d10ef | 2026-10-26 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-26 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-26 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 9f712b9 (árvore suja) | 2026-10-26 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- O E2E de filtros da agenda ("Esta semana", SPEC-0025) não falha mais quando roda no domingo à noite: os shows da semana usam horários ainda futuros (23:58 e 23:59 entre 22:30 e 23:39) e, nos últimos 20 minutos de domingo, o teste é pulado com o motivo escrito.
- Só testes mudaram; o produto (API, web) e a regra "show no passado é recusado" seguem iguais.

### Como foi feito
- TDD com papéis separados: Test-writer (ef7f487) → Implementer (b5f2a86) → Reviewer: APPROVED (nits H42-01 resolvido no texto da spec, H42-02 opcional não aplicado, H42-03 registrado em Pendências).
- Função pura `weekShowTimes(now)` em `e2e/tests/agenda/filters.ts`, no fuso `America/Sao_Paulo`, ao lado de `daysUntilSunday` e `dayMonth`; o teste chama `test.skip(condição, motivo)`.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- Antes: no G1 a UT-01 falhou em ef7f487 (`TypeError: weekShowTimes is not a function`); o PR #52 falhou no domingo 2026-10-25 entre 23:30 e 23:59 com "A data e a hora do show já passaram." (run 40256479624). Depois: passa em b5f2a86 e na integração (16d10ef, G5).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | `weekShowTimes(now)`: domingo 23:45 de São Paulo → `null`; domingo 23:05 → 23:58 e 23:59; domingo 15:00 e quarta 23:50 → 23:00 e 23:30 | PASS | `gate G2 --run` em b5f2a86; `gate G5 --run` na integração (16d10ef); CI do PR #54 (run 40363048130, checks verdes em 9f712b9) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — CI verde no PR #54 (run 40363048130)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG atualizado
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — nenhuma

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas); mudança só de teste. Entrega na `main` pelo PR #54 (https://github.com/thomasravache/subsolo/pull/54), merge commit b1d321f em 2026-10-26, com H2 pela política auto-on-green.

### Pendências
- H42-03 (pré-existente, fora do escopo): fora do domingo, depois das 23:55, um show das 23:30 de hoje já passou, e um teste que atravesse a meia-noite pode ver o dia mudar no meio. Risco baixo; vira spec se aparecer no CI.
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
