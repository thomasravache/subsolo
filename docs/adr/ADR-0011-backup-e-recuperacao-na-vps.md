---
id: ADR-0011
title: Backup e recuperação na VPS
status: accepted
origin: decision
date: 2026-10-13
pillars: [resiliencia]
baseline: resiliencia@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: job de restauração automática mensal com resultado no Seq + testes de integração de timeout e idempotência
---

# ADR-0011 — Backup e recuperação na VPS

## Contexto e Problema
Backup que nunca foi restaurado não é backup, e falhas de dependências externas derrubam o sistema inteiro quando não há limites. No Subsolo, tudo roda numa única VPS: se o disco ou o provedor falhar, a agenda da cena e os seguidores somem junto, a menos que haja cópia fora dela.

## Direcionadores da Decisão
- Uma VPS (escolha do usuário), sem banco gerenciado.
- Dados importantes, mas não financeiros: perder até 24 h é aceitável; ficar fora algumas horas também.
- Custo próximo de zero.

## Opções Consideradas
- `pg_dump` diário + flyers, enviados com **restic** (criptografado e deduplicado) para object storage S3 compatível fora da VPS (Cloudflare R2)
- Snapshot do disco pelo provedor da VPS
- Replicação contínua (WAL archiving com pgBackRest)

## Resultado da Decisão
**Opção escolhida:** `pg_dump` diário (03h) de produção + os flyers (`/media`), enviados com restic para um bucket Cloudflare R2, com retenção de 7 diários, 4 semanais e 6 mensais. O snapshot do provedor, se existir, é uma camada extra, nunca a única. O WAL archiving fica para quando o RPO precisar ser menor.

- **Quando:** o backup e o teste de restauração são implantados no épico de go-live, junto com a produção (ADR-0012); antes disso não há dado real a proteger.
- **RPO:** 24 h. **RTO:** 4 h (VPS nova provisionada pelo script de `infra/` + restauração do último backup + deploy da última tag).
- **Teste de restauração:** um job mensal restaura o último backup num container descartável, confere as contagens de tabelas e de arquivos e registra o resultado no Seq; falha gera alerta.
- **Staging nunca recebe dados de produção** (dados pessoais; ADR-0008): usa dados sintéticos.

**Regras (verificáveis):**
- Backup automático diário com restauração testada todo mês.
- RPO de 24 h e RTO de 4 h.
- Chamadas externas (e-mail, push, Google) com timeout e retry com backoff (`Microsoft.Extensions.Http.Resilience`, já nos ServiceDefaults); nunca espera indefinida.
- Operações repetíveis são idempotentes: o consumo de eventos usa inbox, e cada aviso tem chave única (usuário + show + tipo), para nunca avisar duas vezes.
- Falha de dependência não crítica degrada a funcionalidade em vez de derrubar o sistema: se o e-mail ou o push estiver fora, os avisos ficam na fila e a agenda continua no ar.

### Consequências
- **Boa**, porque uma falha total da VPS é recuperável em horas, com custo zero dentro do plano gratuito do R2.
- **Ruim**, porque até 24 h de cadastros podem se perder no pior caso, e a recuperação é manual (seguindo um runbook).

### Confirmação (G3)
Teste de restauração mensal registrado no Seq e ensaiado uma vez, manualmente, no walking skeleton; testes de integração para timeout, retry e idempotência dos avisos.

## Prós e Contras das Opções
| Critério (peso) | pg_dump + restic + R2 | Snapshot do provedor | pgBackRest/WAL |
|---|---|---|---|
| Sobrevive à perda do provedor (3) | sim | não | sim |
| RPO (2) | 24 h | conforme o provedor | minutos |
| Complexidade (3) | baixa | mínima | alta |
| Custo (2) | zero até 10 GB | varia | zero até 10 GB |

## Mais Informações
- Cloudflare R2: 10 GB-mês, 1 M de operações classe A e 10 M classe B gratuitos; US$ 0,015/GB-mês acima disso; sem custo de saída — https://developers.cloudflare.com/r2/pricing/ (verificado em 2026-10-13).
- restic (BSD-2) — https://restic.net (não reverificado nesta data).
- Referências neutras: Google SRE; Well-Architected (pilar de confiabilidade).
