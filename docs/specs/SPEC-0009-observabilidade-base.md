---
id: SPEC-0009
title: Observabilidade base
tier: full
type: foundation
user_facing: true
status: approved
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0008]
consumes_contract: []
contract_version: 1
touches: [src/Subsolo.ServiceDefaults/**, src/Common/Subsolo.Common.Infrastructure/Observability/**, web/instrumentation.ts, web/src/observability/**, web/package.json, web/pnpm-lock.yaml, Directory.Packages.props, infra/seq/**, docs/runbooks/erros-5xx.md, docs/runbooks/outbox-parada.md, tests/Observability/**, e2e/tests/observability/**]
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

**Decisão:** toda a instrumentação passa por `Subsolo.ServiceDefaults` e por `web/instrumentation.ts`; os módulos não configuram telemetria por conta própria.

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
Log (OTLP → Seq), campos obrigatórios:
  @t, @l, @m, trace_id, span_id, service.name (api|web), module, feature, environment
Mascaramento: e-mail → "j***@d***.com" ; token/Authorization/Cookie → "[REDACTED]"
Alertas Seq (infra/seq/alerts.json):
  "Erros 5xx": count(status >= 500) > 5 em 5 min → e-mail ao responsável → docs/runbooks/erros-5xx.md
  "Falha de job": evento "OutboxProcessingFailed" ≥ 3 em 10 min → docs/runbooks/outbox-parada.md
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

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um registro de log com `email=joao@exemplo.com`, `Authorization: Bearer abc` e um cookie, quando passa pelo redator, então o e-mail sai como `j***@e***.com` e o token e o cookie como `[REDACTED]`.
- **UT-02** — Dado o health check da outbox com a última execução há 61 s (FakeTimeProvider), quando avaliado, então retorna Unhealthy; com 59 s, Healthy.

### 7.3 Testes de Integração
- **IT-01** — Dados o AppHost (Aspire.Hosting.Testing) e um coletor OTLP de teste, quando `/cidade-exemplo` é requisitada, então existe um trace com spans de `web`, `api` e `postgres` sob o mesmo `trace_id`.
- **IT-02** — Dado um endpoint de teste que loga um e-mail e recebe um cookie, quando chamado, então nenhum log ou atributo de span exportado contém o e-mail ou o cookie em texto puro.
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
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0009`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
