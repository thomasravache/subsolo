---
id: SPEC-0036
title: Ajustes dos jobs agendados de avisos
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-24
parent: SPEC-0002
depends_on: [SPEC-0032]
consumes_contract: []
touches: [infra/seq/alerts.json, docs/runbooks/**, tests/Observability/Subsolo.Observability.UnitTests/**, src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Modules/Events/**, tests/Modules/Events/**, web/src/components/shows/**, e2e/tests/avisos/**]
adrs: [ADR-0010, ADR-0014]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-24
---

# SPEC-0036 — Ajustes dos jobs agendados de avisos

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão G4 da SPEC-0032 (itens `H32-NN`):
- **H32-01** — o alerta "Resumo diário falhou" (`count >= 1` em 1 h) passou a cobrir também `DayBeforeReminderRunFailed` e `NoticesRetentionRunFailed`; o título engana e o runbook `outbox-parada.md` ainda diz que o alerta é só do resumo, "uma vez por dia" (o resumo agora tem a recuperação a cada 15 min).
- **H32-02** — `CatchUpDailyDigestCommand.DigestHour = 9` é fixo, mas `Engagement:DigestCron` é configurável: com o cron depois das 9h a recuperação roda antes, todo dia, com aviso `DailyDigestCatchingUp`, e o cron configurado é ignorado em silêncio.
- **H32-03** — `ShowPage.tsx`: o espaço entre a contagem e o texto seguinte ficou dentro do bloco da contagem; o comentário do teste H29-03 diz "sem o nó de texto solto", o que não é literal.
- **H32-04** — o lembrete faz um `GetAttendeesAsync` por show (≈14 vezes por véspera) e a consulta de hora em hora de `events.shows` por `status + starts_at_utc` não tem índice que a sirva (o `ix_shows_city_starts_at` exige a cidade).
- **H32-05** — `DailyDigestCatchUpTests.TheRegularTriggerRecordsTodaysRun` e o E2E-01 da SPEC-0032 podem falhar perto da meia-noite de São Paulo.

## 2. Causa Raiz
- H32-01: o alerta foi reaproveitado na SPEC-0032 sem renomear (`infra/seq/alerts.json`, `docs/runbooks/outbox-parada.md:26`).
- H32-02: a hora da recuperação não deriva do cron (`CatchUpDailyDigestCommand.cs:25`).
- H32-03: o regex de contagem do teste da SPEC-0029 lê `textContent` e precisa de espaço depois do número.
- H32-04: `ReminderSelector.cs:36` itera os shows; o índice da SPEC-0023 é por cidade.
- H32-05: as asserções comparam a data do relógio de parede do Quartz com `UtcNow` sem tolerar a virada do dia.

## 3. Mudança Proposta
- H32-01: renomear o alerta para "Job agendado falhou" (mesmo filtro, `count >= 1` em 1 h, eventos `DailyDigestRunFailed`, `DayBeforeReminderRunFailed`, `NoticesRetentionRunFailed`) e corrigir o runbook (três eventos, sem "uma vez por dia"). "Falha de job" fica para os jobs de alta frequência.
- H32-02: a API não sobe se `Engagement:DigestCron` não dispara às 09h00 de São Paulo (validação na subida com `CronExpression.IsSatisfiedBy` às 09h00 de São Paulo do dia corrente pelo relógio da aplicação; "0 * * * * ?" da pilha local continua válido); comentário junto de `CronSetting`.
- H32-03: o espaço vai para dentro do `<p>` do `Attendance` (sem nó de texto só com espaço no bloco) e o comentário do teste fica exato.
- H32-04: `IAttendanceApi.GetAttendeesAsync(IReadOnlyCollection<Guid> showIds)` em lote (contrato interno entre módulos, sem rota pública) retornando `Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>>` (show → quem vai, no molde de `GetShowStatesAsync`), usado pelo `ReminderSelector`; índice `ix_shows_scheduled_starts_at` em `events.shows(starts_at_utc) where status = 'scheduled'`.
- H32-05: a asserção aceita o dia seguinte; o E2E-01 do lembrete espera passar a meia-noite de São Paulo se faltar menos de 3 min.
- **Não muda:** textos dos avisos, chaves, horários padrão, rotas públicas e OpenAPI. O contador `subsolo.engagement.reminders` continua contando reexecuções (a descrição já diz isso; aceito na revisão).

**Padrão seguido:** `infra/seq/alerts.json` e testes da SPEC-0031/0032 em `tests/Observability/Subsolo.Observability.UnitTests/**`; validação de opções na subida como `Engagement:ReminderHour` (SPEC-0032); `GetShowStatesAsync` em lote (SPEC-0032, H29-01); migrações do Events.

**Rollback:** reverter o merge commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área coberta pelos testes das SPEC-0031 e 0032.
- **UT-01** — Dado `infra/seq/alerts.json` e o runbook, quando lidos, então existe o alerta "Job agendado falhou" com `count >= 1` em 1 h cobrindo os três eventos, nenhum alerta se chama "Resumo diário falhou", e o runbook lista os três eventos sem "uma vez por dia". (H32-01)
- **UT-02** — Dada a configuração `Engagement:DigestCron` = "0 0 10 * * ?", quando o módulo valida as opções na subida, então falha com mensagem que cita `DigestCron` e as 9h; com "0 0 9 * * ?" e "0 * * * * ?" passa. (H32-02)
- **UT-03** — Dada a página do show com 3 "vou", quando renderizada, então nenhum bloco da seção "Ingresso" tem nó de texto só com espaço e o texto da contagem ("3 vão") vem seguido de espaço dentro do mesmo parágrafo. (H32-03)
- **IT-01** — Dados 200 shows na janela, quando o lembrete roda, então `GetAttendeesAsync` é chamado uma vez em lote e o índice `ix_shows_scheduled_starts_at` existe em `pg_index`. (H32-04)
- E2E: N/A — H32-05 só endurece testes existentes contra a virada do dia (sem comportamento novo).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-23, "pode continuar de onde parou no modo automático com o recomendado até chegarmos na parte do deploy").

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
- [ ] Red: UT-01..UT-03 e IT-01 com a tag `SPEC-0036:<ID>`, falhando pelo motivo certo
- [ ] Green: alerta e runbook, validação do DigestCron, espaço no `Attendance`, lote e índice, asserções da virada do dia
- [ ] Validar: build + suíte + arquitetura (G2/G3)
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 6e2b15d (árvore suja) | 2026-10-24 |
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
| 1 (sem mudança de contrato público) | 2026-10-24 | H32-02: a validação checa as 09h00 de São Paulo do dia corrente (relógio da aplicação). H32-04: o lote retorna `IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>>`. | retorno do Test-writer (pontos 1 e 3) | — | piloto automático do Thomas (2026-10-24) |
