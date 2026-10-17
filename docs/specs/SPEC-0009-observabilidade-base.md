---
id: SPEC-0009
title: Observabilidade base
tier: full
type: foundation
user_facing: true
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0008]
consumes_contract: []
contract_version: 2
touches: [Subsolo.slnx, .github/workflows/ci.yml, src/Common/Subsolo.Common.Application/Decorators/LoggingDecorator.cs, src/Common/Subsolo.Common.Infrastructure/Messaging/ProcessMessagesJob.cs, src/Common/Subsolo.Common.Infrastructure/Inbox/ProcessInboxJob.cs, src/Subsolo.AppHost/**, src/Common/Subsolo.Common.Infrastructure/CommonInfrastructureExtensions.cs, tools/compose-up.sh, e2e/playwright.config.ts, src/Subsolo.ServiceDefaults/**, src/Common/Subsolo.Common.Infrastructure/Observability/**, web/src/instrumentation.ts, web/src/observability/**, web/package.json, web/pnpm-lock.yaml, Directory.Packages.props, infra/seq/**, docs/runbooks/erros-5xx.md, docs/runbooks/outbox-parada.md, tests/Observability/**, tests/Common/Subsolo.Common.IntegrationTests/Observability/**, e2e/tests/observability/**]
adrs: [ADR-0010, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0009 — Observabilidade base

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Liga a observabilidade do ADR-0010 de ponta a ponta, localmente: logs estruturados e traces da API e do Next.js exportados por OTLP para o Seq, com o mesmo trace id do navegador ao banco; health checks reais (banco e jobs); mascaramento de dados pessoais nos logs; e os primeiros alertas do Seq (erros 5xx e falha de job da outbox) com runbook. O monitor externo e o Seq de produção ficam para o go-live (SPEC-0003).

## 2. Motivação & Escopo
**Motivação:** sem sinais confiáveis desde o começo, os primeiros problemas reais (aviso que não sai, página lenta) só seriam descobertos pelos fãs.

**Objetivos (dentro do escopo):**
- API: ServiceDefaults com Serilog ou `ILogger` + exportador OTLP para o Seq; enriquecimento com `trace_id`, `module`, `feature`.
- Next.js: instrumentação OpenTelemetry (`instrumentation.ts`) exportando para o mesmo coletor, propagando `traceparent` nas chamadas à API.
- Health checks: `/health` (PostgreSQL + job da outbox executou nos últimos 60 s) e `/alive` (processo).
- Redator de dados pessoais: e-mails, tokens e cabeçalhos `Authorization`/`Cookie` mascarados em logs e atributos de trace.
- Seq: retenção de 30 dias; sinais salvos "Erros 5xx" e "Falha de job"; alertas por e-mail (Mailpit no local) — configuração como código em `infra/seq/`.
- Teste das atividades da outbox/inbox (fonte `Subsolo.Messaging`, criada na SPEC-0007) e da assinatura das fontes no ServiceDefaults (minor N3 da revisão da SPEC-0007).
- Runbooks em `docs/runbooks/`: `erros-5xx.md` e `outbox-parada.md`.

**Não-objetivos (fora do escopo):**
- Monitor externo, Seq de produção e alertas reais por e-mail (SPEC-0003).
- SLO de avisos (entra com o módulo Notifications no épico SPEC-0002).

## 3. Dependências
- **Implementações necessárias:** SPEC-0008 — a jornada da agenda (e, por transitividade, a base da SPEC-0007) precisa estar rodando para os testes ponta a ponta.
- **Contratos consumidos:** N/A — a rota `/{cidade}` e o endpoint da agenda (contrato v1 da SPEC-0008) vêm de uma implementação já em `depends_on`; o CT-01 garante que a instrumentação não altera esse contrato.
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0010 (OpenTelemetry → Seq) e ADR-0008 (sem dado pessoal em logs).

**Decisão:** toda a instrumentação passa por `Subsolo.ServiceDefaults` e por `web/src/instrumentation.ts`; os módulos não configuram telemetria por conta própria.

**Justificativa:** um ponto único garante o mesmo formato e o mascaramento em todos os módulos.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** sink direto do Serilog para o Seq (perde o padrão OTLP e prende ao Seq); logs só no console (sem busca nem alerta).

**ADRs:** ADR-0010, ADR-0008

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** overhead da instrumentação < 5% no p95 da agenda — comparado no E2E-02 da SPEC-0008, com e sem exportação.
- **Segurança:** o Seq local não fica exposto fora do localhost; no Compose, sem porta publicada (acesso por túnel) — IT-04.
- **Privacidade e dados pessoais:** e-mail, token e cookie nunca aparecem em logs ou traces — UT-01, IT-02.
- **Disponibilidade e resiliência:** com o Seq fora do ar, a API continua respondendo (a exportação descarta sem bloquear) — IT-03.
- **Acessibilidade (UI):** N/A — sem tela nova.
- **Custo:** zero; Seq Individual local (ADR-0010/ADR-0012).

## 6. Artefato A — Contrato
**Interface:** `sinais de observabilidade e health checks`

```text
GET /health  → 200 {status: Healthy} | 503 {status: Unhealthy, entries: {postgres, outbox}}
GET /alive   → 200 enquanto o processo responde
Log da API (OTLP → Seq), campos obrigatórios:
  @t, @l, @m, trace_id, span_id, service.name=api, deployment.environment,
  module e feature quando o log vem de um handler de módulo (ex.: module=events, feature=GetCityAgenda)
Spans (api|web): service.name e deployment.environment no resource
  (o web exporta só spans; logs estruturados do web entram com o login, épico SPEC-0002)
Mascaramento: e-mail → "j***@d***.com" ; token/Authorization/Cookie → "[REDACTED]"
Alertas Seq (infra/seq/alerts.json):
  "Erros 5xx": count(status >= 500) > 5 em 5 min → e-mail ao responsável → docs/runbooks/erros-5xx.md
  "Falha de job": evento (EventName) "OutboxProcessingFailed" ≥ 3 em 10 min → docs/runbooks/outbox-parada.md
    emitido quando uma mensagem da outbox ou da inbox falha ao ser processada e quando o job inteiro falha
Redação falha fechado: se o mascaramento não puder ser concluído (ex.: timeout da regex), o valor sai "[REDACTED]"
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Trace ponta a ponta | fã abre `/{cidade}` | um trace com spans web → api → postgres | IT-01, E2E-01 |
| Mascaramento | log com e-mail, token e cookie | valores mascarados | UT-01, IT-02 |
| Seq fora do ar | Seq parado | API responde normalmente | IT-03 |
| Health saudável | banco ok e outbox recente | `/health` 200 | IT-05 |
| Health degradado | banco parado ou outbox parada > 60 s | `/health` 503 com a entrada falha | UT-02, IT-05 |
| Alerta de 5xx | 6 erros 500 em 5 min | alerta disparado (e-mail no Mailpit) | IT-06 |
| Seq não exposto | Compose gerado | serviço Seq sem porta publicada | IT-04 |
| Campos do log | log de um handler de módulo | `module`, `feature`, `deployment.environment` e `service.name` presentes | UT-03 |
| Mascaramento além da mensagem | exceção, escopo dicionário, valor não-string ou evento de span com e-mail | valores mascarados; regex em timeout → `[REDACTED]` sem lançar | UT-04, IT-02 |
| Falha de processamento de mensagem | handler da outbox ou da inbox lança, ou o job inteiro falha | evento `OutboxProcessingFailed` com a exceção; o filtro do alerta casa pelo nome do evento | UT-05 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um registro de log com `email=joao@exemplo.com`, `Authorization: Bearer abc` e um cookie, quando passa pelo redator, então o e-mail sai como `j***@e***.com` e o token e o cookie como `[REDACTED]`.
- **UT-02** — Dado o health check da outbox com a última execução há 61 s (FakeTimeProvider), quando avaliado, então retorna Unhealthy; com 59 s, Healthy.
- **UT-03** — Dado um log emitido dentro de um handler do módulo Events (ex.: `GetCityAgenda`) com o ambiente `Development`, quando exportado (exportador em memória), então o registro traz `module=events`, `feature=GetCityAgenda`, e o resource traz `service.name=api` e `deployment.environment=Development` (Emenda 4).
- **UT-04** — Dados (a) uma exceção cuja mensagem contém `joao@exemplo.com`, (b) um escopo `Dictionary<string, object?>` com `email`, (c) um valor não-string (`Uri` com e-mail) e (d) um texto patológico que estoura o timeout da regex, quando passam pelo redator de logs e pelo processor de spans (incluindo eventos `exception` de `activity.AddException`), então nenhum e-mail sai em texto puro e (d) sai `[REDACTED]` sem lançar exceção (Emenda 4).
- **UT-05** — Dado o job de processamento da outbox (e o da inbox) com um handler que sempre lança, e dado um job que falha por inteiro, quando executados, então é emitido um log com `EventName=OutboxProcessingFailed` contendo a exceção, e o filtro de "Falha de job" em `infra/seq/alerts.json` seleciona pelo nome do evento, não pelo texto da mensagem (Emenda 4).

### 7.3 Testes de Integração
- **IT-01** — Dados o AppHost (Aspire.Hosting.Testing) e um coletor OTLP de teste, quando `/cidade-exemplo` é requisitada, então existe um trace com spans de `web`, `api` e `postgres` sob o mesmo `trace_id`.
- **IT-02** — Dado um endpoint de teste que loga um e-mail e recebe um cookie, quando chamado, então nenhum log ou atributo de span exportado contém o e-mail ou o cookie em texto puro. Inclui um endpoint que lança uma exceção com e-mail na mensagem: nem o log nem o evento de span exportados contêm o e-mail (Emenda 4).
- **IT-03** — Dado o endpoint OTLP apontando para um destino inexistente, quando 50 requisições são feitas à agenda, então todas respondem 200, sem aumento de latência acima de 5%.
- **IT-04** — Dado o `docker-compose.yaml` gerado pelo `aspire publish`, quando inspecionado, então o serviço `seq` não publica portas no host.
- **IT-05** — Dado o PostgreSQL parado (container pausado), quando `/health` é chamado, então retorna 503 com a entrada `postgres` Unhealthy; ao retomar, volta a 200.
- **IT-06** — Dado o Seq local com `infra/seq/alerts.json` aplicado, quando 6 respostas 500 são geradas em 5 minutos, então um e-mail de alerta chega ao Mailpit com o link do runbook.

### 7.4 Testes de Contrato
- **CT-01** — Dado o contrato SPEC-0008@1, quando a rota `/{cidade}` e o endpoint `/api/v1/cities/{slug}/agenda` são chamados com instrumentação ativa, então as respostas continuam idênticas ao contrato (status e schema) — a observabilidade não altera o comportamento consumido.

### 7.5 Testes E2E
- **E2E-01** — Dado o app subido pelo AppHost, quando o fã abre a agenda da cidade no projeto mobile, então a página carrega normalmente e o Seq local registra o trace da navegação, consultado pela API do Seq pelo `trace_id` do cabeçalho `traceparent` [jornada: agenda].

### 7.6 Outros
- N/A

**Dublês e dados de teste:** coletor OTLP em memória para os testes; cidade seed `cidade-exemplo`.

**Ambiente de execução:** local e CI (Testcontainers + Aspire.Hosting.Testing).

## 8. Plano de Rollout
- **Estratégia:** deploy direto (sem produção até a SPEC-0003).
- **Dados/schema:** N/A
- **Compatibilidade:** comportamento do contrato SPEC-0008@1 inalterado (CT-01).
- **Observabilidade:** esta spec é a própria base de observabilidade; os alertas e runbooks ficam versionados.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Redação e health checks**
- [ ] Red: UT-01, UT-02, IT-02, IT-05 com a tag `SPEC-0009:<ID>`, falhando pelo motivo certo
- [ ] Green: redator de dados pessoais nos ServiceDefaults; health checks de PostgreSQL e da outbox
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 2: Telemetria ponta a ponta e alertas**
- [ ] Red: IT-01, IT-03, IT-04, IT-06, CT-01 e E2E-01 falhando
- [ ] Green: OTLP da API e do Next.js (`instrumentation.ts`) para o Seq, propagação de `traceparent`, `infra/seq/alerts.json`, runbooks
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte + arquitetura + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — ff83806 | 2026-10-16 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — f4ce5e4 | 2026-10-16 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 040ms) — f4ce5e4 | 2026-10-16 |
| G4 Review | FAIL | Reviewer independente: REPROVADO em f4ce5e4 — B1 campos module/feature/environment do contrato não emitidos; I1 exceções, escopos e valores não-string escapam do redator (logs e eventos de span); I2 alerta 'Falha de job' não cobre falha por mensagem nem inbox e não tem teste; I2b timeout da regex lança dentro do log | 2026-10-16 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-16 | G4 | spec | Revisão G4: contrato pede module/feature/environment nos logs sem teste que o prove; redação não cobre exceções, escopos dicionário, valores não-string nem eventos de span; alerta 'Falha de job' não cobre falha por mensagem/inbox e sem teste; regex com timeout pode lançar no log | Implementer entregou G2 verde; lacunas do plano de testes | Architect | Emenda 4 — contrato v2 e testes UT-03, UT-04, UT-05 + caso no IT-02 | 2026-10-16 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0009`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-16 | Emenda 4 (revisão G4 reprovada, IMP-01): contrato v2 — campos dos logs da API explicitados (`module`/`feature` vindos do handler, `deployment.environment` e `service.name` no resource; o web exporta só spans até o login do épico SPEC-0002); o evento `OutboxProcessingFailed` passa a ser emitido também na falha de cada mensagem da outbox e da inbox, e o alerta filtra pelo nome do evento; o redator falha fechado (`[REDACTED]` em timeout) e cobre exceções, escopos dicionário, valores não-string e eventos de span. Novos testes UT-03, UT-04, UT-05 e caso extra no IT-02; entram nos `touches` o `LoggingDecorator` (module/feature) e os jobs `ProcessMessagesJob` e `ProcessInboxJob` (evento por mensagem), sem mudar o comportamento de retry da SPEC-0007. Achados menores levados ao Implementer na mesma rodada: runbook com URL absoluta e "Erros 5xx" só da API, propagador só `tracecontext` no web, `SEQ_PORT` validado no compose-up, E2E-01 pulado com motivo quando `BASE_URL` é externo, `SensitiveKeyParts` sem closure. Registro: em run mode o Seq recebe a telemetria de api e web no lugar do dashboard do Aspire (decisão local, revisar no ADR-0010 junto da SPEC-0003); retenção, alertas, autenticação e o app de e-mail do Seq em produção, ruído de spans de polling e heartbeat por módulo ficam para a SPEC-0003 e o épico SPEC-0002 | G4 reprovado (B1, I1, I2, I2b) | SPEC-0002, SPEC-0003 | thomas (delegado: piloto automático, 2026-10-16, recomendação do Architect) |
| 1 (escopo e testes) | 2026-10-16 | Emenda 3 (TEST_DEFECT do Implementer): o Next 16 trava `web/.next/dev/lock`, então só um AppHost com `next dev` pode rodar por vez; o assembly `Subsolo.Observability.AppHostTests` desliga o paralelismo entre classes (`[assembly: Xunit.v3.Parallelization(Mode = ParallelMode.None)]`, o equivalente no xUnit v3 4.x, onde `CollectionBehavior(DisableTestParallelization)` está obsoleto, mudança do Test-writer), e os comandos `test` e `test_integration` do `sdd-config.yml` passam a usar `--max-parallel-test-modules 1` (o que também evita o OOM da máquina de 8 GB); os jobs `test` e `integration` de `.github/workflows/ci.yml` acompanham os comandos (o contrato `CiWorkflowCommandsContractTests` exige o mesmo texto), por isso o arquivo entra nos `touches`. Registros: o redator de dados pessoais fica nos ServiceDefaults da API, como diz o escopo; o web ainda não trata dado pessoal e ganha redator quando o login entrar (épico SPEC-0002); o alerta por e-mail usa o app `Seq.App.EmailPlus` 5.0.0, instalado pelo Seq a partir do nuget.org em tempo de execução (a instalação no Seq de produção fica na SPEC-0003) | Corrida no `test_commands_contract` e no `coverage` ("Another next dev server is already running") | SPEC-0005 (comandos do sdd-config), SPEC-0008 (AppHost.IntegrationTests passa a rodar sem concorrência), SPEC-0002, SPEC-0003 | thomas (delegado: piloto automático, 2026-10-16, recomendação do Architect) |
| 1 (escopo e testes) | 2026-10-16 | Emenda 2 (impedimentos do Test-writer): `Subsolo.slnx` entra nos `touches` (projetos de `tests/Observability`); o E2E-01 chega ao Seq por um override só local do `tools/compose-up.sh` que publica o Seq em `127.0.0.1` e exporta `SEQ_URL` (o artefato publicado continua sem porta do Seq, IT-04); o heartbeat da outbox é registrado por um `IJobListener` do Quartz em `Observability/**` e o health check em `CommonInfrastructureExtensions.cs`, sem tocar a mensageria; o AppHost entra para ligar o OTLP de api/web ao Seq, aplicar os alertas e o SMTP do Mailpit; o redator de dados pessoais vale também para os logs de console (ADR-0008), com um caso a mais no UT-01; os testes herdados das atividades da mensageria ficam com a tag `SPEC-0009:IT-01` | IMP 1–4 do Test-writer | SPEC-0008 (`tools/compose-up.sh` ganha o override local; o artefato não muda), SPEC-0010 (AppHost também tocado; o Integrator resolve o merge) | thomas (delegado: piloto automático, 2026-10-16) |
| 1 (escopo) | 2026-10-16 | Emenda 1 (preparação do Architect): o app Next fica em `web/src/`, então a instrumentação é `web/src/instrumentation.ts` | Estrutura real do projeto (SPEC-0008) | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
