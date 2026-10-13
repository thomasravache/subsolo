---
id: ADR-0005
title: Estratégia de testes
status: accepted
origin: decision
date: 2026-10-13
pillars: [testes]
baseline: testes@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: comandos test, test_integration, test_e2e, arch_test e coverage do sdd-config, no CI
---

# ADR-0005 — Estratégia de testes

## Contexto e Problema
Sem uma estratégia explícita, os testes ficam desbalanceados (muitos E2E instáveis ou nenhum teste de integração real) e o CI deixa de ser confiável. No Subsolo, o código é escrito em grande parte por agentes de IA, e o que protege o produto é a suíte: regras de agenda, permissões, avisos e prévias de compartilhamento.

## Direcionadores da Decisão
- Back-end .NET 10 (ADR-0001), front Next.js (ADR-0013) e PostgreSQL real (ADR-0002).
- Uso quase todo no celular: as jornadas E2E rodam em viewport móvel.
- Uma pessoa revisa: os testes precisam ser rápidos e determinísticos.
- CI gratuito com limite de minutos (ADR-0004).

## Opções Consideradas
- xUnit v3 + Testcontainers + Playwright + Vitest
- NUnit + banco em memória (EF InMemory/SQLite) + Cypress

## Resultado da Decisão
**Opção escolhida:** xUnit v3 + Testcontainers (PostgreSQL real) + Playwright + Vitest, porque testam contra dependências reais, rodam iguais no local e no CI, e o Playwright cobre a emulação móvel e as prévias de compartilhamento. O banco em memória foi descartado por esconder diferenças de SQL, de fuso e de schema.

**Regras (verificáveis):**
- Níveis e obrigatoriedade conforme a skill: unitário, integração com dependências reais, contrato entre specs e E2E das jornadas de usuário.
- A suíte completa roda no CI em toda solicitação de mudança.
- As jornadas críticas estão em `critical_journeys` no sdd-config e cada uma tem ao menos um E2E.
- Teste instável é defeito: vai para quarentena por no máximo 7 dias com spec aberta para corrigir; nunca retry-até-passar.
- Cobertura das linhas alteradas ≥ 80% quando a medição estiver configurada.
- Tempo controlado nos testes: relógio injetado (`TimeProvider`), nunca `DateTime.Now`; o "lembrete na véspera" é testado com tempo falso.
- Asserções com Shouldly (BSD-2); FluentAssertions 8+ tem licença comercial e não é usado.

**Ferramenta por nível:**
| Nível | Ferramenta / abordagem | Por quê | Onde roda |
|---|---|---|---|
| Unitário | xUnit v3 (4.0.2) + Shouldly 4.3 no .NET; Vitest 5 + Testing Library no front | regras de domínio e componentes rápidos, sem I/O | local + CI |
| Integração | xUnit v3 + `WebApplicationFactory` + Testcontainers.PostgreSql 4.16 (PostgreSQL 18 real em container) + servidor SMTP de teste em container para e-mail | prova endpoints, EF, migrações, outbox/inbox e jobs contra o banco real | local + CI |
| Contrato | (1) OpenAPI gerado pela API com snapshot versionado, e o cliente TypeScript gerado (openapi-typescript) precisa compilar no CI; (2) testes de serialização dos eventos de integração de cada módulo | quebra de contrato front↔API ou entre módulos falha no CI | CI |
| E2E | Playwright 1.64 (TypeScript) contra a aplicação completa subida pelo Aspire/Compose, em viewport de celular e desktop | jornadas reais: cadastrar show, agenda, página com Open Graph, seguir, "vou" | local + CI + staging (smoke) |
| Carga | N/A — sem meta de desempenho além de latência simples nos RNFs; perfil de pilares `padrao` | — | — |
| Acessibilidade | @axe-core/playwright 4.13 nas páginas públicas (agenda, show, perfil) | visual escuro exige contraste verificado | CI |

### Consequências
- **Boa**, porque os testes de integração pegam problemas de SQL, fuso e migração que mocks esconderiam, e os E2E provam as jornadas no celular.
- **Ruim**, porque testes com containers são mais lentos (minutos de CI); a mitigação é reaproveitar o container por coleção de testes e limpar as tabelas entre testes com `TRUNCATE` por schema.

### Confirmação (G3)
Comandos `test`, `test_integration`, `test_e2e`, `arch_test` e `coverage` do sdd-config, executados pelo `gate --run` e pelo CI.

## Prós e Contras das Opções
| Critério (peso) | xUnit v3 + Testcontainers + Playwright | NUnit + banco em memória + Cypress |
|---|---|---|
| Fidelidade ao banco real (3) | alta | baixa |
| Emulação móvel e várias abas (2) | nativa no Playwright | limitada |
| Velocidade (2) | média | alta |
| Licença (2) | toda aberta | aberta |

## Mais Informações
- xunit.v3 4.0.2; Testcontainers.PostgreSql 4.16.0; Shouldly 4.3.0 — https://www.nuget.org (API https://api.nuget.org/v3-flatcontainer/, verificado em 2026-10-13).
- @playwright/test 1.64.0; vitest 5.0.4; @axe-core/playwright 4.13.0; openapi-typescript 7.13.0 — https://registry.npmjs.org (verificado em 2026-10-13).
- Referências neutras: pirâmide de testes; references/testing.md da skill.
