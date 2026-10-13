---
id: ADR-0004
title: Aspire, Docker Compose e Caddy na VPS via GitHub Actions
status: accepted
origin: user
date: 2026-10-13
pillars: [entrega]
baseline: entrega@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: pipeline GitHub Actions (deploy.yml) + comandos deploy_staging/deploy_production/smoke_test do sdd-config
---

# ADR-0004 — Aspire, Docker Compose e Caddy na VPS via GitHub Actions

## Contexto e Problema
Sem um caminho automatizado até produção, o produto fica "pronto" sem nunca chegar ao usuário, e cada deploy vira um evento de risco. O Thomas escolheu hospedar numa **VPS própria** e usar o **Aspire** para orquestrar a aplicação; a forma de levar o código até a VPS (GitHub Actions + Docker Compose + Caddy) foi recomendação aceita.

## Direcionadores da Decisão
- Escolhas do usuário (origin: user): VPS própria, Aspire, GitHub.
- Uma pessoa operando: deploy e rollback em um passo, sem painel extra para manter.
- Staging e produção isolados, mesmo na mesma VPS.
- HTTPS automático no domínio `agendasubsolo.com.br`.

## Opções Consideradas
- Aspire (AppHost) + publicação em Docker Compose + Caddy, com deploy por GitHub Actions via SSH (recomendada e aceita)
- Coolify ou Dokploy na VPS
- Kamal 2

## Resultado da Decisão
**Opção escolhida:** Aspire 13.6 como modelo único da aplicação: em desenvolvimento, `aspire run` sobe a API, o Next.js, o PostgreSQL e o Seq; no deploy, o `AddDockerComposeEnvironment` gera o `docker-compose.yaml` e os `.env` por ambiente. O GitHub Actions monta as imagens, aplica as migrações (bundle EF) e atualiza a VPS por SSH. O Caddy, na frente, faz o TLS automático e o roteamento.

- **Fases (escolha do Thomas, ADR-0012):** a fundação entrega tudo rodando localmente (`aspire run`), mais o CI gratuito no GitHub e a geração do Compose de deploy (`aspire publish`), validada localmente subindo o Compose gerado. A VPS, o domínio, o DNS, o TLS real e os deploys de staging e produção são feitos no épico de go-live, depois que o Thomas aprovar os custos. Até lá, o G6 das specs fica N/A, apontando para esse épico.
- **Ambientes:** `staging.agendasubsolo.com.br` e `agendasubsolo.com.br`, como projetos Compose separados na mesma VPS (rede, banco e volumes próprios). Desenvolvimento local via `aspire run`.
- **Roteamento (Caddy):** `/api/*` → API .NET; `/media/*` → arquivos de flyer (ADR-0015); o resto → Next.js. Mesmo domínio = cookie de sessão sem CORS (ADR-0003).
- **Imagens:** marcadas com o SHA do commit; publicação no GHCR (gratuito para o repositório público `subsolo-br/subsolo`, ADR-0007).
- **Fluxo:** merge em `main` → CI verde → deploy automático em staging → smoke + E2E contra staging → produção com aprovação manual no *environment* `production` do GitHub (confirmação humana por release).
- **Rollback:** rodar de novo o deploy com a tag anterior (`workflow_dispatch` com `version=<sha>`); migrações são sempre compatíveis com N-1 (ADR-0002).
- **VPS como código:** script de provisionamento versionado (`infra/`: usuário de deploy, Docker, firewall, Caddy, cron de backup), idempotente, para recriar o servidor do zero (ADR-0011).

**Regras (verificáveis):**
- Um único pipeline leva do commit à produção; nada é publicado fora dele.
- A branch principal está sempre em estado publicável.
- Staging é atualizado automaticamente a cada merge, com smoke e E2E contra ele.
- Produção exige confirmação humana por release; a estratégia (direta ou feature flag) é definida por spec.
- Rollback em um passo, ensaiado no walking skeleton; mudanças de dados compatíveis com a versão anterior.
- Métricas DORA acompanhadas a partir do histórico do GitHub Actions: frequência de deploy, tempo de entrega, taxa de falha e tempo de recuperação.

### Consequências
- **Boa**, porque a mesma definição (AppHost) roda localmente e gera o deploy, sem painel para manter, com TLS automático e rollback por tag.
- **Ruim**, porque staging e produção compartilham a VPS (um pico ou uma falha de disco afeta os dois), e o deploy tem uma janela de segundos de indisponibilidade ao trocar containers (aceitável para o produto; blue-green pode vir depois).
- **Ruim**, porque a publicação em Docker Compose do Aspire é recente; se algum recurso não for suportado, o compose gerado é ajustado via `PublishAsDockerComposeService`/`ConfigureComposeFile`.

### Confirmação (G3)
Pipeline GitHub Actions (`.github/workflows/ci.yml` e `deploy.yml`); comandos `deploy_staging`, `deploy_production` e `smoke_test` no sdd-config (G6); rollback ensaiado e registrado no walking skeleton.

## Prós e Contras das Opções
| Critério (peso) | Aspire + Compose + Caddy | Coolify/Dokploy | Kamal 2 |
|---|---|---|---|
| Itens para manter na VPS (3) | Docker + Caddy | + painel, banco e agente do painel | Docker + kamal-proxy |
| Coerência com Aspire/.NET (3) | total | parcial | baixa (Ruby) |
| Zero downtime (1) | não nativo | sim | sim |
| Rollback simples (3) | tag anterior | pelo painel | `kamal rollback` |

## Mais Informações
- Aspire 13.6.0 (2026-09-29); deploy em Docker Compose com `aspire publish` / `aspire deploy` — https://aspire.dev/deployment/docker-compose/ (verificado em 2026-10-13).
- Aspire.Hosting.Docker 13.6.1, Aspire.Hosting.JavaScript 13.6.1 e Aspire.Hosting.PostgreSQL 13.6.1 — nuget.org (verificado em 2026-10-13).
- GitHub Actions: 2.000 min/mês em repositórios privados no plano Free, gratuito em públicos; armazenamento de artefatos e pacotes de 500 MB compartilhado — https://docs.github.com/en/billing/concepts/product-billing/github-actions (verificado em 2026-10-13).
- Referências neutras: DORA (Accelerate), Twelve-Factor App.
