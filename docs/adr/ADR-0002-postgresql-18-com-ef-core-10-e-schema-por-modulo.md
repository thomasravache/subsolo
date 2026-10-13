---
id: ADR-0002
title: PostgreSQL 18 com EF Core 10 e schema por módulo
status: accepted
origin: decision
date: 2026-10-13
pillars: [dados-migracoes]
baseline: dados-migracoes@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: teste de integração de migrações (Testcontainers) + teste de arquitetura de schema por DbContext
---

# ADR-0002 — PostgreSQL 18 com EF Core 10 e schema por módulo

## Contexto e Problema
Uma migração de schema acoplada ao deploy quebra o rollback e derruba versões antigas que ainda estão rodando. O Subsolo guarda dados relacionais (shows ↔ bandas ↔ casas ↔ cidades, seguidores, "vou") numa única VPS, com módulos isolados (ADR-0001) e consultas por data, cidade e estilo.

## Direcionadores da Decisão
- Dados fortemente relacionais e consultas por intervalo de data e cidade.
- Isolamento por módulo (um schema por módulo), exigido pelo ADR-0001.
- Roda em container na VPS, com backup simples (ADR-0011).
- Suporte de primeira classe no .NET/EF Core e no Aspire.

## Opções Consideradas
- PostgreSQL 18 + EF Core 10 (Npgsql)
- SQL Server 2025 Express em container
- MySQL 8.4 LTS / MariaDB

## Resultado da Decisão
**Opção escolhida:** PostgreSQL 18 + EF Core 10 com o provider Npgsql, porque é gratuito, leve, tem schemas nativos para isolar módulos, oferece busca textual e `unaccent` em português, e conta com o melhor suporte no Aspire (`Aspire.Hosting.PostgreSQL` 13.6.1).

- Um schema por módulo (`users`, `profiles`, `events`, `engagement`, `notifications`, `moderation`); cada `DbContext` tem o seu `HasDefaultSchema` e o seu histórico de migrações.
- Migrações EF Core versionadas em `<M>.Infrastructure/Migrations`, empacotadas como **bundle** (`dotnet ef migrations bundle`) e executadas pelo pipeline como um passo antes da troca de versão, nunca na subida da aplicação em produção.
- Datas e horas: `timestamptz` em UTC e o fuso da cidade (ex.: `America/Sao_Paulo`) guardado na cidade; o horário do show é exibido no fuso da cidade.

**Regras (verificáveis):**
- Migrações versionadas no repositório e executadas pelo pipeline.
- Toda migração é compatível com a versão anterior da aplicação (expand → migrar → contract).
- Migrações são testadas em teste de integração contra um PostgreSQL 18 real (Testcontainers).
- Cada migração destrutiva tem plano de rollback de dados documentado na spec.
- Nenhum módulo lê ou escreve no schema de outro.

### Consequências
- **Boa**, porque o isolamento por schema mantém as fronteiras do ADR-0001 no banco, e extrair um módulo depois é mover um schema.
- **Ruim**, porque consultas que cruzam módulos (ex.: a agenda com nomes de bandas) exigem leitura replicada via eventos ou composição na API, nunca `JOIN` entre schemas.

### Confirmação (G3)
Teste de integração que aplica todas as migrações num PostgreSQL 18 (Testcontainers) e verifica que a versão N-1 do schema continua compatível; teste de arquitetura que confere `HasDefaultSchema` por `DbContext`; Plano de Rollout das specs com mudança de schema.

## Prós e Contras das Opções
| Critério (peso) | PostgreSQL 18 | SQL Server Express | MySQL/MariaDB |
|---|---|---|---|
| Custo/licença (3) | gratuito, open source | gratuito com limite de 50 GB | gratuito |
| Schemas por módulo (3) | nativo | nativo | "schema" = database |
| Suporte EF Core/Aspire (3) | excelente | excelente | bom |
| Leveza na VPS (2) | leve | pesado (RAM) | leve |
| Busca em português (2) | `unaccent` + FTS | FTS | limitada |

## Mais Informações
- PostgreSQL 18: minor 18.6, suportado até 2030-11-14 — https://www.postgresql.org/versions.json (verificado em 2026-10-13).
- Microsoft.EntityFrameworkCore 10.0.13; Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 — nuget.org (verificado em 2026-10-13).
- Referência neutra: padrão expand/contract (parallel change).
