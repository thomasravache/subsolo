---
id: SPEC-0032
title: Lembrete na véspera
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0029, SPEC-0030, SPEC-0031]
consumes_contract: []
contract_version: 3
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Engagement/**, tests/Modules/Engagement/**, src/Modules/Events/**, tests/Modules/Events/**, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, tests/**/packages.lock.json, src/Subsolo.AppHost/**, e2e/tests/avisos/**, e2e/tests/fixtures.ts, web/src/components/shows/**, src/Modules/Notifications/**, tests/Modules/Notifications/**, src/Common/Subsolo.Common.Presentation/Security/**, tests/Common/**, tests/Security/**, tests/Subsolo.ArchitectureTests/**, infra/seq/alerts.json, docs/runbooks/**, tests/Observability/Subsolo.Observability.UnitTests/**, tools/compose-up.sh, web/src/components/notifications/**, tests/Modules/Profiles/Subsolo.Profiles.UnitTests/Infrastructure/ProfilesHarness.cs]
adrs: [ADR-0001, ADR-0014]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0032 — Lembrete na véspera

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Na véspera do show, a partir das 10h no fuso da cidade, quem marcou "vou" recebe um lembrete ("Amanhã: Cova Rasa + Peste Negra · 22h00 · Basement") por push e/ou e-mail, conforme as preferências (SPEC-0030). Shows cancelados não geram lembrete; um show adiado ganha lembrete na véspera da nova data.

## 2. Motivação & Escopo
**Motivação:** ADR-0014 (lembrete de véspera às 10h no fuso da cidade, para quem marcou "vou"; cancelados e adiados de fora); fecha o ciclo do "vou" (SPEC-0029).

**Objetivos (dentro do escopo):**
- Job do Quartz `DayBeforeReminder` no `Engagement`, de hora em hora (`Engagement:ReminderCron`, padrão "0 0 * * * ?"): para cada show `scheduled` cujo início cai no dia seguinte do fuso da cidade, e com a hora local da cidade ≥ `Engagement:ReminderHour` (padrão 10), busca quem marcou "vou" (`GetAttendeesAsync`, SPEC-0029) e chama `EnqueueAsync` com `Notice(reminder, "reminder:{showId}:{início em UTC yyyyMMddHHmm}", "Amanhã: {atrações}", "{HH'h'mm} · {casa}, {cidade}", "/{cidade}/{slug}")`.
- `IEventsCatalog` ganha `ListScheduledShowsStartingAsync(fromUtc, toUtc)` (resumos da SPEC-0031).
- Rodar de novo na mesma janela não duplica (chave); se a API ficou fora às 10h, a execução das 11h envia.

**Não-objetivos (fora do escopo):**
- Aviso de cancelamento ou de mudança de data.
- Lembrete no dia do show ou com antecedência configurável.

## 3. Dependências
- **Implementações necessárias:** SPEC-0029 (`GetAttendeesAsync`), SPEC-0030 (`EnqueueAsync` e preferências) e SPEC-0031 (`ShowSummary` no `IEventsCatalog`).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** N/A.

## 4. Decisão Arquitetural
**Contexto:** ADR-0014 com o esclarecimento de 2026-10-18; ADR-0001; Quartz no `Common.Infrastructure`; job e consumidor da SPEC-0031 como implementação de referência.

**Decisão:** job `DayBeforeReminder` no `Engagement`, seguindo o `DailyDigest` da SPEC-0031; a chave inclui o início, então um adiamento gera um lembrete novo e o antigo nunca sai (o show da data velha não está mais na janela).

**Justificativa:** ADR-0014 (horário, público e idempotência), ADR-0001 (direção das dependências).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** um gatilho por show agendado no Quartz (muitos gatilhos para manter quando a data muda); job uma vez por dia às 10h de Brasília (erra o fuso das cidades fora dele).

**ADRs:** ADR-0001, ADR-0014

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** uma execução com 200 shows na janela e 5.000 "vou" em ≤ 30 s (IT-03).
- **Segurança:** nenhuma entrada pública nova.
- **Privacidade e dados pessoais:** nada novo é guardado; o aviso não cita outros fãs.
- **Disponibilidade e resiliência:** de hora em hora com chave idempotente; uma execução perdida é coberta pela seguinte.
- **Acessibilidade (UI):** N/A — sem tela; e-mail no modelo da SPEC-0030.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** job `DayBeforeReminder` · `IEventsCatalog.ListScheduledShowsStartingAsync` · aviso `reminder`

```csharp
// Subsolo.Modules.Events.IntegrationEvents — IEventsCatalog ganha:
Task<IReadOnlyList<ShowSummary>> ListScheduledShowsStartingAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);
```

```text
Janela por execução: shows scheduled com início entre agora e agora + 48 h; filtra os que começam "amanhã" no fuso da cidade e cuja cidade já passou de ReminderHour hoje
Aviso reminder: título "Amanhã: {Lineup separado por ' + '}" ; corpo "{HH'h'mm no fuso} · {VenueName}, {CityName}" ; url "/{CitySlug}/{Slug}" ; chave "reminder:{ShowId}:{StartsAtUtc yyyyMMddHHmm}"
Config: Engagement:ReminderCron (padrão "0 0 * * * ?") ; Engagement:ReminderHour (padrão 10)
```

**Ajustes de contrato (Emenda 2, contrato v2):**
```csharp
// IEventsCatalog ganha também (H29-01): estados em lote, só dos ids encontrados
Task<IReadOnlyDictionary<Guid, ShowState>> GetShowStatesAsync(IReadOnlyCollection<Guid> showIds, CancellationToken ct);
```
```text
Resumo diário com recuperação (H31-01):
  tabela engagement.digest_runs (digest_date date PK, completed_at_utc timestamptz) — uma linha por dia (America/Sao_Paulo) com resumo concluído
  gatilho DigestCron (padrão "0 0 9 * * ?"): janela (horário agendado − 24 h, horário agendado]; ao concluir grava digest_runs do dia
  gatilho de recuperação Engagement:DigestCatchUpCron (padrão "0 0/15 * * * ?"): se a hora em São Paulo ≥ 9 e não há digest_runs do dia,
    roda com a janela (9h de hoje − 24 h, 9h de hoje]; senão não faz nada. A chave digest:{data} por fã evita e-mail duplicado.
  falha em qualquer gatilho: DailyDigestRunFailed (como hoje); a recuperação seguinte tenta de novo. Runbook outbox-parada.md descreve a recuperação.
Contadores (H31-05): subsolo.engagement.digest_recipients e subsolo.engagement.new_show_notices passam a ter a descrição "seguidores considerados (inclui reexecuções)"; o log DailyDigestCompleted diz "fã(s) considerado(s)".
NoticesRetention (H31-04): falha vira NoticesRetentionRunFailed (Error), incluído no filtro do alerta "Falha de job" e no runbook.
Retenção (H31-07): índice ix_deliveries_created_at_utc em notifications.deliveries(created_at_utc), por migração do Notifications.
CSRF (H31-03): lista permitida de endpoints com CsrfExemptMetadata = { UnsubscribeNoticeEmailOneClick }, todos AllowAnonymous.
Nomes (Emenda 3): job Quartz Engagement.DayBeforeReminder; seletor ReminderSelector.SelectAsync(reminderHour, ct); falha do job → DayBeforeReminderRunFailed (Error), no filtro do alerta "Falha de job" e no runbook.
Recuperação (Emenda 3): o gatilho de recuperação é observável só pela config (DigestCron, DigestCatchUpCron) e pela tabela digest_runs; antes das 9h em São Paulo não faz nada. Corte da line-up (H31-08): no máximo 200 unidades UTF-16, em limite de elemento de texto.
Pilha local (E2E-01): tools/compose-up.sh e o AppHost passam Engagement__ReminderCron="0 * * * * ?" e Engagement__ReminderHour="0"; os padrões de produção não mudam.
```

**Design:** N/A — push do sistema e e-mail no modelo da SPEC-0030; sem tela nova.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Véspera, depois das 10h | show amanhã em Curitiba; 10h05 em Curitiba | lembrete para quem marcou "vou" | UT-01, IT-01, E2E-01 |
| Véspera, antes das 10h | 09h55 em Curitiba | nada ainda | UT-01 |
| Fuso diferente | show em Manaus (America/Manaus) | usa 10h de Manaus | UT-01 |
| Repetição | job roda às 10h e às 11h | um lembrete por fã | IT-02 |
| Cancelado | cancelado antes das 10h da véspera | nenhum lembrete | IT-01 |
| Adiado | data muda depois do lembrete | lembrete novo na véspera da nova data; nenhum na data antiga | UT-01, IT-02 |
| Ninguém vai | nenhum "vou" | nada enfileirado | UT-01 |
| Escala | 200 shows, 5.000 "vou" | ≤ 30 s | IT-03 |
| Falha do job | banco fora durante a execução | `DayBeforeReminderRunFailed` (Error), alerta "Falha de job"; a próxima hora tenta de novo | IT-04 |
| Contrato | `IEventsCatalog` | assinatura do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — job novo; "vou" e shows seguem cobertos pelas SPEC-0023 e 0029.

### 7.2 Testes Unitários
- **UT-01** — Dado o seletor de lembretes com `TimeProvider` fixo e shows em Curitiba (America/Sao_Paulo) e Manaus (America/Manaus), quando roda às 09h55 e às 10h05 locais, um dia antes, então seleciona só depois das 10h de cada cidade, ignora show sem "vou", monta o texto e a chave do §6, e um show adiado gera a chave da nova data.

### 7.3 Testes de Integração
- **IT-01** — Dados a API, o PostgreSQL, um show amanhã com três fãs que marcaram "vou" e outro show cancelado com "vou", quando o job roda depois das 10h locais, então há entregas `reminder` para os três fãs (pelos canais que ligaram) e nenhuma do cancelado.
- **IT-02** — Dado o mesmo cenário, quando o job roda às 10h e às 11h e depois o show é adiado uma semana e o job roda na nova véspera, então cada fã tem uma entrega na primeira véspera e uma na nova, nenhuma a mais.
- **IT-03** — Dados 200 shows na janela e 5.000 "vou", quando o job roda, então termina em ≤ 30 s com uma entrega por fã e show.
- **IT-04** — Dado o job `DayBeforeReminder` com o banco indisponível durante a execução, quando roda, então registra `DayBeforeReminderRunFailed` (Error) sem propagar a exceção ao Quartz, e o filtro do alerta "Falha de job" em `infra/seq/alerts.json` inclui o evento.

### 7.4 Testes de Contrato
- **CT-01** — Dado o tipo público `IEventsCatalog`, quando lido por reflexão, então `ListScheduledShowsStartingAsync` tem a forma do §6; o snapshot OpenAPI não muda.

### 7.5 Testes E2E
- **E2E-01** — Dada a pilha do Compose com `Engagement:ReminderCron` a cada minuto e `Engagement:ReminderHour` = 0: um artista publica um show para amanhã; o fã logado marca "vou" e liga "Lembrete na véspera (por e-mail)"; em até 2 minutos chega ao Mailpit o e-mail "Amanhã: …" com o link do show, que abre a página dele. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Itens herdados da revisão da SPEC-0029 (G4, tag `SPEC-0032:H29-NN`):**
  - H29-01 — `ListMyAttendancesQueryHandler`: uma chamada `GetShowStateAsync` por "vou" marcado, que cresce com o tempo (o `AttendButton` chama em toda página de show); trocar por leitura em lote no `IEventsCatalog` (`GetShowStatesAsync(ids)`) ou filtrar pela data de início numa consulta só, com teste.
  - H29-02 — `AttendShowCommandHandler`: o PUT que perde a corrida não incrementa o contador `subsolo.engagement.attendances`; documentar no código (sem mudança de comportamento).
  - H29-03 — `ShowPage.tsx`: trocar o nó de texto `{" "}` depois da contagem por separação estrutural (contagem num bloco próprio).

- **Itens herdados da revisão da SPEC-0031 (G4, tag `SPEC-0032:H31-NN`):**
  - H31-01 — `SendDailyDigestCommand`: a janela fixa de 24 h perde os shows do dia se a execução das 9h falhar ou a API estiver fora às 9h (Quartz em memória, sem recuperação); usar a janela desde a última execução com sucesso (limitada a 7 dias) ou repetir a cada 15 min até dar certo no dia (a chave `digest:{data}` evita duplicar), com teste, e ajustar o runbook `outbox-parada.md`.
  - H31-02 — `SendDailyDigestCommand`: um `EnqueueAsync` por corpo distinto de resumo; o teste de volume do resumo da SPEC-0031 só mede o melhor caso (todos com o mesmo corpo). Acrescentar a variante com corpos diferentes (fãs espalhados por N bandas) dentro de ≤ 60 s, ou agrupar os avisos numa ida ao banco.
  - H31-03 — `AntiforgeryFilter.ExemptFromCsrf`: teste de arquitetura ou segurança que lista os endpoints com `CsrfExemptMetadata`, compara com a lista permitida (hoje só `UnsubscribeNoticeEmailOneClick`) e exige `AllowAnonymous`; teste unitário do ramo isento.
  - H31-04 — `NoticesRetentionJob`: try/catch com o evento `NoticesRetentionRunFailed`, incluído no alerta "Falha de job" e no runbook, com teste.
  - H31-05 — contadores `subsolo.engagement.digest_recipients` e `subsolo.engagement.new_show_notices` (e o log `DailyDigestCompleted`): contar só o que foi enfileirado com sucesso, sem recontar reexecuções do dia, ou renomear a descrição para "seguidores considerados", com teste.
  - H31-06 — `DigestBuilder`: caso de UT para o corte de 1.000 caracteres (linhas que não cabem vão para "e mais {n}").
  - H31-07 — `NoticesRetentionJob`: índice em `deliveries.created_at_utc` (ou exclusão em lotes) para o `delete` da retenção não varrer a tabela.
  - H31-08 — `NoticeText`: cortar a line-up em limite de elemento de texto (`StringInfo`), sem partir par substituto (emoji no nome da banda), com teste.
**Dublês e dados de teste:** `TimeProvider` fixo nos UT e IT; dados criados no banco do teste.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL, Mailpit); E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** tabela `engagement.digest_runs` (Emenda 2) e índice `ix_deliveries_created_at_utc` no `notifications`; as entregas do lembrete ficam no `notifications`.
- **Compatibilidade:** `IEventsCatalog` ganha um método.
- **Observabilidade:** contador `subsolo.engagement.reminders`; falha do job vira alerta no Seq (ADR-0010).
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Lembrete de show adiado? — Sim, na véspera da nova data, pela chave que inclui o início (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado").

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Regras**
- [ ] Red: UT-01 com a tag `SPEC-0032:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Job e contrato**
- [ ] Red: IT-01..IT-03, CT-01 falhando pelo motivo certo
- [ ] Green: `ListScheduledShowsStartingAsync` e job `DayBeforeReminder`
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: cron e hora do lembrete configuráveis na pilha do E2E

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 6855548 | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — bc5add1 | 2026-10-24 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 44af482 | 2026-10-24 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 11s 570ms) — 44af482 | 2026-10-24 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 blocker/major; 2 minor e 5 nits; H32-01..05 levados à SPEC-0036; §8 corrigido na Emenda 3) — bb4155d | 2026-10-24 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 16m 09s 234ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 10s 595ms) — 3bd5f34 | 2026-10-24 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-24 |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0032`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-24 | `touches` e §7.6 ganham os itens H31-01..08 herdados da revisão G4 da SPEC-0031 (Notifications, Common.Presentation/Security, alertas do Seq, runbooks e testes correspondentes). | revisão G4 da SPEC-0031 | — | piloto automático do Thomas (2026-10-24) |
| 2 | 2026-10-24 | Decisões concretas dos itens herdados: `IEventsCatalog.GetShowStatesAsync` (H29-01); resumo com recuperação por `engagement.digest_runs` e gatilho `Engagement:DigestCatchUpCron` a cada 15 min (H31-01); descrição dos contadores (H31-05); `NoticesRetentionRunFailed` (H31-04); índice da retenção (H31-07); lista permitida de isenção de CSRF (H31-03); cron e hora do lembrete na pilha local; `touches` ganha `tools/compose-up.sh` e `web/src/components/notifications/**`. | despacho do Test-writer: itens herdados sem decisão concreta | SPEC-0031 (implementada; nenhum consumidor externo) | piloto automático do Thomas (2026-10-24) |
| 3 | 2026-10-24 | Retorno do Test-writer: novo IT-04 (`DayBeforeReminderRunFailed` e alerta "Falha de job"); nomes `Engagement.DayBeforeReminder` e `ReminderSelector.SelectAsync`; recuperação do resumo observável por config e `digest_runs`; corte da line-up em ≤ 200 unidades UTF-16; texto do H31-02 sem ID solto; `touches` ganha `ProfilesHarness.cs` (implementa `IEventsCatalog`). | SD-1..SD-5 do Test-writer | — | piloto automático do Thomas (2026-10-24) |
| 3 (sem mudança de contrato) | 2026-10-24 | §8 passa a citar a tabela `engagement.digest_runs` e o índice da retenção; achados menores da revisão G4 (H32-01..05) vão para a SPEC-0036. | revisão G4 | SPEC-0036 | piloto automático do Thomas (2026-10-24) |
