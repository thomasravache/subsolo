---
id: ADR-0009
title: Dependências e ciclo de vida da stack
status: accepted
origin: decision
date: 2026-10-13
pillars: [dependencias]
baseline: dependencias@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: security_scan no CI (dotnet list package --vulnerable + pnpm audit + gitleaks) + Dependabot
---

# ADR-0009 — Dependências e ciclo de vida da stack

## Contexto e Problema
Dependências vulneráveis ou fora de suporte viram risco silencioso, e atualizar tudo de uma vez depois fica caro. O Subsolo tem dois ecossistemas (NuGet e npm), imagens Docker (PostgreSQL, Caddy, Seq) e já nasce com uma lição registrada no ADR-0001: bibliotecas populares mudam de licença (MediatR, MassTransit, FluentAssertions).

## Direcionadores da Decisão
- Ferramentas gratuitas no GitHub.
- Uma pessoa revisando: atualizações em lotes pequenos e agrupados.
- Licenças compatíveis com um produto gratuito, mantido por uma marca.

## Opções Consideradas
- Dependabot (version + security updates) + `dotnet list package --vulnerable` + `pnpm audit`
- Renovate (GitHub App)

## Resultado da Decisão
**Opção escolhida:** Dependabot, porque é nativo do GitHub, gratuito em repositórios públicos e privados, e cobre NuGet, npm, Docker e GitHub Actions sem instalar nada.

- NuGet com Central Package Management (`Directory.Packages.props`) e `packages.lock.json` (`RestorePackagesWithLockFile`).
- Front com pnpm e `pnpm-lock.yaml`; Node.js 24 LTS ("Krypton").
- Imagens Docker com tag de versão fixa (ex.: `postgres:18.6`), nunca `latest`.
- Dependabot semanal, com grupos (minor/patch juntos), e os PRs passam pelo fluxo normal (ADR-0007).
- Calendário de fim de suporte: .NET 10 (2028-11-14), PostgreSQL 18 (2030-11-14) e Node 24 LTS, revisado a cada 6 meses; cada migração de versão vira spec.

**Regras (verificáveis):**
- Varredura de vulnerabilidades no CI; severidade alta ou crítica bloqueia o merge.
- Atualização de dependências automatizada (Dependabot) com revisão, pelo menos semanal.
- Nenhuma dependência ou runtime fora de suporte (EOL); o calendário de fim de suporte é revisado a cada 6 meses e cada item vira spec.
- Lockfiles versionados (`packages.lock.json`, `pnpm-lock.yaml`).
- Licenças compatíveis: permitidas MIT, Apache-2.0, BSD, ISC, PostgreSQL e MPL-2.0; LGPL só para binários nativos ligados dinamicamente (ex.: libvips); qualquer licença comercial, RPL ou "split license" exige ADR.

### Consequências
- **Boa**, porque as vulnerabilidades aparecem no PR, e o risco de licença é tratado antes de entrar.
- **Ruim**, porque os PRs do Dependabot geram trabalho semanal de revisão (mitigado com agrupamento).

### Confirmação (G3)
`security_scan` no sdd-config e no CI; `verify` avisa manifesto alterado sem ADR; `.github/dependabot.yml` versionado.

## Prós e Contras das Opções
| Critério (peso) | Dependabot | Renovate |
|---|---|---|
| Instalação (2) | nativo | GitHub App |
| Agrupamento e agendamento (2) | bom | excelente |
| Cobertura NuGet/npm/Docker/Actions (3) | sim | sim |

## Mais Informações
- Node.js 24.21.0 LTS "Krypton" (2026-09-07); a linha atual é a 26.11.1 — https://nodejs.org/dist/index.json (verificado em 2026-10-13).
- pnpm 12.11.2 — registry.npmjs.org (verificado em 2026-10-13).
- Referências neutras: SLSA, OpenSSF Scorecard.
