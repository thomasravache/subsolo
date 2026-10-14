---
id: ADR-0007
title: Fluxo de mudança por pull request no GitHub
status: accepted
origin: decision
date: 2026-10-13
pillars: [fluxo-mudanca]
baseline: fluxo-mudanca@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: proteção da branch main no GitHub + change_flow pr no sdd-config + job sdd (pr-check) obrigatório
---

# ADR-0007 — Fluxo de mudança por pull request no GitHub

## Contexto e Problema
Push direto e merges locais tiram a revisão e os checks do caminho, e a rastreabilidade entre spec, código e deploy se perde. No Subsolo, quem escreve o código são agentes e quem revisa e aprova é uma única pessoa (Thomas), no GitHub.

## Direcionadores da Decisão
- Repositório no GitHub, acompanhamento só no repositório (escolha do usuário).
- Uma pessoa revisando o trabalho dos agentes.
- Plano gratuito: no GitHub Free, a proteção de branch só existe em repositório **público**; em privado ela exige o GitHub Pro.

## Opções Consideradas
- Repositório público com proteção de branch (gratuita)
- Repositório privado + GitHub Pro para ter proteção de branch
- Repositório privado no Free, sem proteção (a disciplina fica só nos hooks e no CI)

## Resultado da Decisão
**Opção escolhida:** pull request obrigatório na `main`, com proteção de branch. **Repositório público `thomasravache/subsolo`** (escolha do Thomas em 2026-10-13; transferido para a organização `subsolo-br` no go-live, SPEC-0003 — Emenda v1 da SPEC-0004): gratuito, com proteção de branch, CodeQL e GHCR sem custo; nenhum segredo fica no código (ADR-0008).

- PR com título em Conventional Commits, gerado por `spec_graph.py pr`; commits com `Refs: SPEC-NNNN`.
- Checks obrigatórios: `build`, `test`, `e2e`, `lint`, `security` e `sdd` (validate + pr-check).
- **Revisão humana:** o GitHub não deixa o autor aprovar o próprio PR. Como os agentes usam a conta do Thomas, a exigência de "1 aprovação" na proteção fica em 0, e a revisão humana é garantida por: (1) só o Thomas faz o merge; (2) o H2 do Thomas registrado na spec e verificado pelo `pr-check`. Se no futuro os agentes passarem a usar um bot (GitHub App), a regra muda para 1 aprovação obrigatória.

- **Autorização permanente (Thomas, 2026-10-13):** o Architect (agente) pode criar PRs e fazer o merge com merge commit, **sempre depois do H2 do Thomas registrado na spec** (aprovação de integração por onda continua humana). Deploy em produção continua exigindo confirmação por release.

**Regras (verificáveis):**
- Toda mudança entra na `main` por pull request.
- Checks obrigatórios (build, testes, `sdd`) e aprovação humana registrada (H2) antes do merge.
- Sem push direto nem force-push na `main`, inclusive para administradores.
- Merge com merge commit (sem squash), para as evidências dos gates continuarem válidas.
- Arquivos de controle (`tools/sdd`, `docs/specs/sdd-config.yml`, `docs/adr`, `.githooks`, `.github/workflows`) têm o Thomas como dono no CODEOWNERS.
- Correção urgente segue o mesmo caminho, com spec lite criada na hora e revisão acelerada.

### Consequências
- **Boa**, porque nada chega a produção sem CI verde, spec aprovada e decisão humana, mesmo com vários agentes.
- **Ruim**, porque, sem um bot, a aprovação humana não é forçada pelo próprio GitHub; ela depende do `pr-check` e de só o Thomas fazer o merge.

### Confirmação (G3)
Proteção de branch no GitHub (ruleset da `main`); `change_flow: pr` no sdd-config; `vendor --codeowners`; `validate` aponta commits diretos.

## Prós e Contras das Opções
| Critério (peso) | Público | Privado + Pro | Privado sem proteção |
|---|---|---|---|
| Proteção de branch (3) | sim, grátis | sim | não |
| Custo (3) | zero | assinatura mensal do Pro | zero |
| Código visível (2) | sim | não | não |
| CodeQL e GHCR grátis (1) | sim | não | não |

## Mais Informações
- Planos do GitHub: proteção de branch em repositórios privados listada só no GitHub Pro — https://docs.github.com/en/get-started/learning-about-github/githubs-plans (verificado em 2026-10-13). Preço do Pro não verificado.
- Referência neutra: práticas de trunk-based development.
