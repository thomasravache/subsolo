---
id: SPEC-0038
title: Ajustes da validação do resumo e do lembrete em lote
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-24
parent: SPEC-0002
depends_on: [SPEC-0036]
consumes_contract: []
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, docs/runbooks/**, web/src/components/engagement/**, web/src/components/shows/**, e2e/tests/avisos/**]
adrs: [ADR-0010, ADR-0014]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-24
---

# SPEC-0038 — Ajustes da validação do resumo e do lembrete em lote

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão G4 da SPEC-0036 (itens `H36-NN`):
- **H36-01** — com o show agendado, a contagem é desenhada pelo `AttendButton`; o espaço dela mudou na SPEC-0036 (beee36e) sem teste, porque o UT-03 só renderiza show cancelado.
- **H36-02** — o `GetAttendeesAsync` em lote trata lista vazia, ids repetidos e show sem "vou" (conjunto vazio), mas nenhum teste cobre esses casos; o IT-01 semeia um "vou" por show.
- **H36-03** — a validação do `Engagement:DigestCron` olha só as 09h00 de São Paulo do dia em que a API sobe: um cron legítimo como `0 0 9 ? * MON-FRI` impede a subida no fim de semana.
- **H36-04** — uma expressão cron inválida recebe a mensagem "deve disparar às 09h00", que esconde o erro de sintaxe.
- **H36-05** — no E2E-01 do lembrete a margem de 3 min antes da meia-noite é menor que o tempo da jornada (até 6 min): começando às 23h56 ela ainda pode cruzar a virada do dia.

## 2. Causa Raiz
- H36-01 e H36-02: testes não acompanharam dois caminhos da implementação (`AttendButton.tsx:127`, `AttendanceApi.cs:29`).
- H36-03: `DigestScheduleOptions.cs:37` checa um único instante (Emenda 1 da SPEC-0036).
- H36-04: `DigestScheduleOptions.cs:26` junta a falha de `TryParse` com a falha de horário.
- H36-05: `lembrete.spec.ts:46` usa margem fixa de 3 min.

## 3. Mudança Proposta
- H36-03: o `DigestCron` é aceito se dispara às 09h00 de São Paulo em pelo menos um dos próximos 7 dias (a partir do dia corrente pelo relógio da aplicação); o comentário junto de `CronSetting` e o runbook dizem isso.
- H36-04: expressão que não passa no `CronExpression.TryParse` falha com mensagem própria: "Engagement:DigestCron (\"…\") não é uma expressão cron válida".
- H36-05: a margem do E2E-01 passa a ser o tempo limite da jornada (o mesmo valor do `test.setTimeout`), não 3 min fixos.
- H36-01 e H36-02: só testes novos, sem mudança de código.
- **Não muda:** horário padrão do resumo (9h), textos dos avisos, rotas, contrato entre módulos.

**Padrão seguido:** `DigestScheduleOptionsValidator` e testes da SPEC-0036 (`DigestCronValidationTests`, `ShowPage.test.tsx`, `DayBeforeReminderBatchTests`); harness `EngagementHarness` dos testes de unidade do Engagement.

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0038:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — área coberta pelos testes das SPEC-0029, 0032 e 0036.
- **UT-01** — Dada a página de um show agendado com 3 "vou", quando renderizada, então nenhum bloco da seção "Ingresso" tem nó de texto só com espaço e o parágrafo da contagem do botão "Vou" casa `/3 vão\s/`. (H36-01)
- **UT-02** — Dado o `AttendanceApi` com "vou" em dois shows, quando `GetAttendeesAsync` recebe lista vazia, então devolve dicionário vazio sem consultar; quando recebe ids repetidos e um show sem "vou", então cada show aparece uma vez e o sem "vou" tem conjunto vazio. (H36-02)
- **UT-03** — Dado o relógio num sábado, quando a API valida `DigestCron` = "0 0 9 ? * MON-FRI", então passa; com "0 0 10 * * ?" falha citando 9h; com "isto não é cron" falha com mensagem de expressão inválida, sem citar 9h. (H36-03, H36-04)
- Integração/E2E: N/A — H36-05 só ajusta a margem de um E2E existente (sem comportamento novo).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-23, "pode continuar de onde parou no modo automático com o recomendado até chegarmos na parte do deploy").

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [ ] Red: UT-01..UT-03 com a tag `SPEC-0038:<ID>`, UT-03 falhando pelo motivo certo (UT-01 e UT-02 cobrem caminhos já corretos e podem passar de primeira)
- [ ] Green: janela de 7 dias, mensagem de cron inválido, margem do E2E, comentário e runbook
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1fd3cd9 (árvore suja) | 2026-10-24 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 123ad19 | 2026-10-25 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — cca7094 | 2026-10-25 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 755ms) — cca7094 | 2026-10-25 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (H38-01 comentário do AddEngagementModule completado em 0338d92; H38-02 informativo: UT-03 no projeto de integração como o teste da SPEC-0036) — 0338d92 | 2026-10-25 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 17m 32s 911ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 14s 741ms) — 6b4c335 | 2026-10-25 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-25 |
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
