---
id: SPEC-0012
title: Ajustes menores do tooling apontados na revisão da SPEC-0004
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: [.gitignore, web/next.config.ts, tests/Subsolo.Tooling.Tests/**]
adrs: [ADR-0005, ADR-0008]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0012 — Ajustes menores do tooling apontados na revisão da SPEC-0004

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
A revisão independente (G4, 2ª rodada) da SPEC-0004 aprovou a entrega e deixou quatro achados minor:
1. `.gitignore` ignora `.env*`, o que também esconde modelos versionáveis como `.env.example`.
2. O UT-01 da SPEC-0004 ainda descreve "Refs: SPEC-NNNN obrigatório" e não cobre os casos aceitos pela Emenda v3: referência à spec fora do rodapé `Refs:` e `[no-spec]`.
3. O teste do pre-commit (IT-02 da SPEC-0004) só usa uma chave falsa no formato AWS; o Mapa de Comportamentos fala em "formato AWS/Google".
4. Comentário de scaffold sobrando em `web/next.config.ts` (`/* config options here */`).
5. A pasta `.claude/`, criada localmente pelo Claude Code (worktrees e locks de sessão), aparece como não rastreada; deve ser ignorada.

## 2. Causa Raiz
Itens 1 e 4: padrões do scaffold e da `.gitignore` inicial não revistos. Itens 2 e 3: a tabela do UT-01 e os casos do IT-02 foram escritos antes da Emenda v3 e com um único formato de chave.

## 3. Mudança Proposta
- `.gitignore`: manter `.env*` ignorado e liberar explicitamente `!.env.example` (e `!**/.env.example`).
- Testes de ferramentas: novas linhas no UT-01 (referência à spec no corpo da mensagem e `[no-spec]` aceitos) e um caso de chave falsa no formato Google API no teste do pre-commit; nova regra no teste de `.gitignore` (`.env.example` não ignorado).
- `web/next.config.ts`: remover o comentário de scaffold.
- `.gitignore`: ignorar `.claude/` (estado local do Claude Code).
- Não muda: o contrato v3 da SPEC-0004, os scripts `tools/build.sh` e `tools/lint.sh` e os hooks.

**Padrão seguido:** `tests/Subsolo.Tooling.Tests` (CommitMessageRuleTests, PreCommitSecretScanTests) e o contrato v3 da SPEC-0004.

**Rollback:** revert do merge commit.

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes da SPEC-0004.
- **UT-01** — Dada a regra do hook commit-msg, quando avaliada com `feat(web): x` + corpo citando `SPEC-0012` fora do rodapé, e com `style: corrige typo [no-spec]`, então ambas são aceitas (exit 0) — guarda do comportamento definido na Emenda v3 da SPEC-0004 (guarda: passa antes da mudança).
- **IT-01** — Dado o `.gitignore` do repositório, quando `git check-ignore --no-index` avalia `.env.example` e `web/.env.example`, então nenhum dos dois é ignorado, enquanto `.env` e `.env.local` continuam ignorados, e `.claude/worktrees/x` passa a ser ignorado (falha antes da correção).
- **IT-02** — Dado o hook pre-commit num repositório temporário, quando um arquivo com chave falsa no formato Google API (`AIza` + 35 caracteres) é commitado, então o commit é bloqueado (guarda: passa antes da mudança).

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Regressão e guardas**
- [ ] Red: IT-01 (`.env.example` e `.claude/` no `.gitignore`) falhando pelo motivo certo; UT-01 e IT-02 escritos como guarda (passam antes da mudança), com a tag `SPEC-0012:<ID>`
- [ ] Green: `.gitignore` com `!.env.example`/`!**/.env.example` e `.claude/`; remover o comentário de scaffold de `web/next.config.ts`
- [ ] Validar: build + suíte completa + lint (G2); G3 N/A até a suíte de arquitetura (SPEC-0007)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live (SPEC-0003)
- [ ] Relatório de Entrega e CHANGELOG (G7)


## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7f944ee (árvore suja) | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 9c0dc7c | 2026-10-16 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 3d8ef04 | 2026-10-16 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 548ms) — 3d8ef04 | 2026-10-16 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 3d8ef04 (9c0dc7c red, 3d8ef04 green); IT-01 red/green confirmado; .gitignore não expõe .env.local/.env.production; chave do IT-02 dispara gcp-api-key só no teste. Minors: summary do CommitMessageRuleTests desatualizado (→ SPEC-0013); .claude/ inteiro ignorado (trocar por .claude/* com exceções se um dia versionarem config) — 3d8ef04 | 2026-10-16 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (texto do plano, sem mudança de contrato) | 2026-10-16 | Exemplo do UT-01 corrigido de `[no-spec] style: corrige typo` para `style: corrige typo [no-spec]` | O hook (contrato v3 da SPEC-0004) exige o tipo no início do assunto; a forma antiga nunca foi aceita, então não seria guarda | Nenhuma | thomas (delegado: piloto automático, 2026-10-16) |
