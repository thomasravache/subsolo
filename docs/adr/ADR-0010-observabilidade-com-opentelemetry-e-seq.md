---
id: ADR-0010
title: Observabilidade com OpenTelemetry e Seq
status: accepted
origin: decision
date: 2026-10-13
pillars: [observabilidade]
baseline: observabilidade@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: ServiceDefaults do Aspire em todos os serviços + teste de integração do health check + monitor externo
---

# ADR-0010 — Observabilidade com OpenTelemetry e Seq

## Contexto e Problema
Sem sinais e metas definidos, os problemas em produção são descobertos pelo usuário, e o rollback vira uma decisão no escuro. No Subsolo, as falhas mais caras são silenciosas: aviso que não sai, lembrete que não chega, prévia de link sem flyer, e a VPS caindo de madrugada.

## Direcionadores da Decisão
- Recomendação aceita pelo Thomas em 2026-10-13: OpenTelemetry → Seq + monitor externo.
- Aspire já fornece OpenTelemetry e health checks via ServiceDefaults.
- Pouca RAM disponível na VPS, que também roda app e banco.
- Uma pessoa recebendo os alertas.

## Opções Consideradas
- OpenTelemetry → Seq na VPS (plano Individual gratuito) + monitor de disponibilidade externo
- Grafana + Loki + Tempo + Prometheus na VPS
- Sentry na nuvem (plano gratuito)

## Resultado da Decisão
**Opção escolhida:** OpenTelemetry (ServiceDefaults do Aspire na API; `@vercel/otel` ou o SDK Node do OpenTelemetry no Next.js) exportando logs e traces por OTLP para um **Seq** na VPS, acessível só por túnel SSH ou por um subdomínio protegido; mais um **monitor externo** (UptimeRobot ou equivalente gratuito) chamando `/health` de staging e produção a cada 5 minutos. O monitor externo e o Seq de produção entram no épico de go-live (ADR-0012); na fundação, o Seq roda local via Aspire. No desenvolvimento, o dashboard do Aspire mostra os mesmos sinais.

**SLI/SLO iniciais:**
| Jornada | SLI | SLO (30 dias) |
|---|---|---|
| Ver a agenda da cidade | % de respostas 2xx/3xx em `/` e `/cidade/*` | 99,5% |
| Página do show e prévia | p95 da renderização no servidor < 800 ms | 95% das requisições |
| Avisos | % de lembretes de véspera enviados até as 12h do dia anterior | 99% |

**Regras (verificáveis):**
- Logs estruturados com ID de correlação (trace id) que atravessa front → API → jobs.
- Métricas de requisição (taxa, erro, duração) e de recurso (CPU, RAM, disco da VPS) para cada serviço.
- Traces distribuídos entre Next.js, API e jobs do Quartz.
- Health check (`/health` e `/alive`) em cada serviço implantável.
- SLI e SLO para as jornadas críticas (tabela acima), com orçamento de erro.
- Alertas do Seq por SLO e por falha de job, enviados por e-mail ao Thomas; cada alerta tem um runbook curto em `docs/runbooks/`.
- Cada Plano de Rollout define o limite numérico que dispara o rollback durante a observação do deploy (padrão: taxa de erro 5xx > 2% por 10 minutos).
- Nenhum dado pessoal ou segredo em logs, métricas ou traces (ADR-0008).

### Consequências
- **Boa**, porque um único lugar concentra logs e traces com busca, o padrão aberto permite trocar o Seq depois, e o monitor externo avisa até quando a VPS inteira cai.
- **Ruim**, porque o Seq Individual permite um usuário e 50 GB (retenção ajustada para 30 dias), e erros do navegador exigem um endpoint próprio de coleta (o Sentry pode entrar depois, ao lado).

### Confirmação (G3)
Instrumentação via `Subsolo.ServiceDefaults` (teste de arquitetura: todo host chama `AddServiceDefaults`); teste de integração do health check; limite de rollback verificado no G6.

## Prós e Contras das Opções
| Critério (peso) | OTel + Seq + monitor externo | Grafana/Loki/Tempo | Sentry nuvem |
|---|---|---|---|
| RAM na VPS (3) | baixa (1 container) | alta (4+ containers) | nenhuma |
| Custo (3) | zero | zero | zero até a cota |
| Logs + traces + alertas (3) | sim | sim | só erros e performance |
| Detecta a VPS fora do ar (2) | sim (monitor externo) | não sozinho | não |

## Mais Informações
- Seq Individual: US$ 0, 1 usuário, 50 GB, alertas incluídos — https://datalust.co/pricing (verificado em 2026-10-13). A página descreve o plano como voltado ao uso individual; uma pessoa operando se encaixa, mas a mudança de equipe exige rever a licença.
- Aspire.Hosting.Seq 13.6.1; OpenTelemetry.Extensions.Hosting 1.19.1 — nuget.org (verificado em 2026-10-13).
- Limites do plano gratuito do UptimeRobot: não verificados nesta data; conferir no SPEC de observabilidade.
- Referências neutras: OpenTelemetry; Google SRE (SLI, SLO, orçamento de erro).
