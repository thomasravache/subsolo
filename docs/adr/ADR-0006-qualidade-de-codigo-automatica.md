---
id: ADR-0006
title: Qualidade de código automática
status: accepted
origin: decision
date: 2026-10-13
pillars: [qualidade-codigo]
baseline: qualidade-codigo@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: comando lint do sdd-config no CI (dotnet format + build com warnings como erro + eslint + tsc + semgrep)
---

# ADR-0006 — Qualidade de código automática

## Contexto e Problema
Estilo e problemas triviais revisados à mão custam caro e geram inconsistência, principalmente quando parte do código é escrita por agentes. No Subsolo, a maior parte do código vem de agentes, e a revisão humana precisa se concentrar em regra de negócio e segurança.

## Direcionadores da Decisão
- Duas linguagens: C# (.NET 10) e TypeScript (Next.js 16).
- Ferramentas gratuitas que funcionem em repositório público ou privado.
- Feedback rápido: local (hook) e no CI.

## Opções Consideradas
- Ferramentas nativas de cada ecossistema: `dotnet format` + analisadores do SDK no .NET; ESLint + Prettier + `tsc --noEmit` no front; Semgrep CE como SAST
- Biome (lint + formatação) no front e SonarCloud como SAST

## Resultado da Decisão
**Opção escolhida:** ferramentas nativas + Semgrep CE, porque são o padrão de cada ecossistema (os agentes as conhecem bem), não dependem de serviço externo e o Semgrep CE funciona em repositório privado sem custo. Como o repositório é público (ADR-0007), o CodeQL entra também, gratuito.

- .NET: `Directory.Build.props` com `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-recommended`, `EnforceCodeStyleInBuild=true`; `.editorconfig` versionado; `dotnet format --verify-no-changes`.
- Front: TypeScript `strict: true` (versão suportada pelo Next.js 16.4), ESLint com a configuração do Next, Prettier, `tsc --noEmit`.
- SAST: Semgrep CE com as regras `p/csharp`, `p/typescript` e `p/secrets`.

**Regras (verificáveis):**
- Formatador com verificação automática no CI (falha se o código não estiver formatado).
- Linter com regras versionadas no repositório; zero avisos novos nas linhas alteradas.
- Checagem de tipos no modo mais estrito viável (nullable no C#, strict no TS).
- Suprimir uma regra exige comentário com o motivo na própria linha; supressão sem motivo é achado do revisor.
- Análise estática de segurança (SAST) no CI.

### Consequências
- **Boa**, porque o revisor não perde tempo com estilo, e os agentes recebem um retorno determinístico.
- **Ruim**, porque warnings como erro deixam o começo mais lento; isso é aceito em troca de não acumular dívida.

### Confirmação (G3)
Comando `lint` do sdd-config no CI e no G2; configuração em `Directory.Build.props`, `.editorconfig`, `web/eslint.config.mjs`, `web/tsconfig.json` e `.semgrep.yml`.

## Prós e Contras das Opções
| Critério (peso) | Nativas + Semgrep CE | Biome + SonarCloud |
|---|---|---|
| Familiaridade dos agentes (3) | alta | média |
| Custo em repositório privado (3) | zero | SonarCloud é pago para privados |
| Velocidade (1) | média | alta (Biome) |

## Mais Informações
- Versões efetivamente adotadas na SPEC-0004 (2026-10-13): ESLint 9.39.5, porque o `eslint-plugin-react` usado pela configuração do Next.js 16.4 só suporta ESLint até 9.x — migrar para o 10 quando o Next suportar (acompanhado pelo Dependabot); TypeScript 6.0.3, porque o typescript-eslint exige TS < 6.1 e o TS 7 não tem a API clássica do compilador.
- eslint 10.12.0; typescript 7.0.2 (verificar a compatibilidade com o Next.js 16.4 no SPEC de repositório; se ainda não for suportado, fixar a última 6.x) — registry.npmjs.org (verificado em 2026-10-13).
- Semgrep Community Edition (LGPL-2.1) — https://semgrep.dev/docs/ (não reverificado nesta data).
- Referência neutra: regras publicadas das próprias ferramentas.
