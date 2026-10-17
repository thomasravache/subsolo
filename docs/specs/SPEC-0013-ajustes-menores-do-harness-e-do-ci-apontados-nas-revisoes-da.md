---
id: SPEC-0013
title: Ajustes menores do harness e do CI apontados nas revisões das SPEC-0005 e SPEC-0006
tier: lite
type: fix
user_facing: false
status: implemented
created: 2026-10-14
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: [src/Common/Subsolo.Common.Domain/DomainEvent.cs, src/Common/Subsolo.Common.Domain/Entity.cs, src/Common/Subsolo.Common.Infrastructure/Messaging/**, src/Common/Subsolo.Common.Infrastructure/Outbox/InsertOutboxMessagesInterceptor.cs, tests/Common/Subsolo.Common.IntegrationTests/**, .gitignore, tests/Subsolo.ArchitectureTests/ArchitectureRules.cs, tests/Subsolo.ArchitectureTests/HarnessExamples/**, tests/README.md, CODEOWNERS, .github/CODEOWNERS, tools/coverage.sh, tools/lint.sh, e2e/static-server.mjs, e2e/package.json, e2e/pnpm-lock.yaml, e2e/tsconfig.json, .github/workflows/ci.yml, tests/Subsolo.Tooling.Tests/**, e2e/tests/**]
adrs: [ADR-0005, ADR-0006, ADR-0009]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-16
approved_via: "delegado: autorização de piloto automático (2026-10-16, seguir as recomendações)"
---

# SPEC-0013 — Ajustes menores do harness e do CI apontados nas revisões das SPEC-0005 e SPEC-0006

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
As revisões independentes (G4) das SPEC-0005 (harness de testes) e SPEC-0006 (CI) aprovaram as entregas e deixaram estes achados minor:
1. `tools/coverage.sh` usa `mapfile` (só existe no bash 4+; o macOS vem com bash 3.2) e expande um array possivelmente vazio sob `set -u`.
2. `tools/coverage.sh` só confere se a base (`origin/main`) existe no fim, depois de rodar toda a suíte (minutos perdidos).
3. `e2e/static-server.mjs` chama `decodeURIComponent` fora de um `try`: uma URL malformada (ex.: `/%E0%A4%A`) derruba o servidor dos testes E2E em vez de responder 400.
4. A pasta `e2e/` (TypeScript do Playwright) fica fora do `tools/lint.sh`: erro de tipo ali só aparece ao rodar os testes.
5. CI (`ci.yml:25-27`): no push na `main`, o grupo de concorrência por `github.ref` deixa o GitHub cancelar um run que está esperando; com merges próximos, alguns commits da `main` ficam sem CI.
6. CI (`ci.yml`, jobs web e e2e): o cache do pnpm usa a mesma chave para `web/` e `e2e/`, que guardam pacotes diferentes; um sempre perde o cache.
7. IT-05 da SPEC-0006 (`GitHubCiVerificationTests.cs:156`) aceita a anotação genérica do próprio workflow ("SDD pr-check"), então passa mesmo se o `pr-check` não explicar o motivo da falha.
8. A supressão de falso positivo do CodeQL por comentário no código nunca foi confirmada para C# e TypeScript com a suíte padrão.

Da revisão das Emendas v6 (SPEC-0005) e v4 (SPEC-0006), em 2026-10-16:
9. `tests/README.md` mostra `test_commands_contract` sem `--max-parallel-test-modules 1` (o sdd-config tem).
10. Nada impede por teste que `test`, `test_integration` ou o filtro padrão do `coverage.sh` voltem a selecionar `Kind=CommandContract` (recursão) nem que o CI rode `test_commands_contract`.
11. O UT-02 da SPEC-0006 não trata `test_commands_contract` como dependente de Node/pnpm.
12. Com `tools/sdd/**` isento de escopo, um PR pode alterar o próprio verificador do gate sem sinal; falta CODEOWNERS ou checagem de que a cópia bate com a versão vendorizada.
13. O comentário de `CiWorkflowCommandsContractTests.cs` ainda cita o contrato SPEC-0005@2 (o atual é @4).
14. A chave do cache do pnpm agora é compartilhada por quatro jobs (agrava o item 6).
16. Revisão da SPEC-0007: `aspire.config.json` gerado na raiz pelo `aspire run` não é ignorado nem versionado.
17. Revisão da SPEC-0007: `ArchitectureRules.OnlyReferences` (SPEC-0005) permite `<Outro>.Contracts`, mas o ADR-0001 diz `IntegrationEvents`.
18. Revisão da SPEC-0007 (N1): o caminho de falha ao gravar o estado da mensagem em `ProcessMessagesJob` não tem teste.
19. Revisão da SPEC-0007 (N2): `DomainEvent.OccurredOnUtc` usa `DateTime.UtcNow` em vez do `TimeProvider`.
20. Revisão da SPEC-0008: nenhum teste de política garante que os jobs `test` e `integration` instalam a Aspire CLI fixada antes de rodar o IT-05 da SPEC-0008 (hoje a prova é o próprio CI).
15. Revisão da SPEC-0012: o `<summary>` de `CommitMessageRuleTests.cs` ainda descreve só `Refs: SPEC-NNNN` (desatualizado após a Emenda v3 da SPEC-0004).

## 2. Causa Raiz
Itens 1–4: escritos e validados só no Linux/bash 5 e no caminho feliz. Itens 5–6: padrões de concorrência e cache copiados para todos os jobs sem considerar push na `main` nem os dois lockfiles. Item 7: asserção fraca demais. Item 8: verificação ficou para depois da primeira execução no GitHub.

## 3. Mudança Proposta
- `tools/coverage.sh`: trocar `mapfile` por leitura em laço compatível com bash 3.2, proteger a expansão de arrays vazios e conferir a base logo no início (falha rápida com a mesma mensagem de hoje).
- `e2e/static-server.mjs`: `decodeURIComponent` dentro de `try`; URL malformada responde 400.
- `e2e/`: `typescript` (mesma versão do `web/`, ADR-0006) como devDependency, script `lint` (`tsc --noEmit`) e `tsconfig.json`; `tools/lint.sh` ganha o passo `e2e-lint`.
- `ci.yml`: grupo de concorrência `ci-<número do PR ou github.sha>` (único por commit no push); chaves de cache `pnpm-store-web-…` e `pnpm-store-e2e-…`, cada uma com o hash do próprio lockfile.
- IT-05 da SPEC-0006: ignorar a anotação "SDD pr-check" e exigir uma anotação que cite a spec ou a regra violada.
- CodeQL: confirmar a supressão por comentário; se não funcionar, documentar no `ci.yml` o caminho real (`query-filters` num config do CodeQL, com o motivo).
- `ProcessMessagesJob`: IT com `SaveChangesInterceptor` que falha uma vez ao gravar o estado (a próxima mensagem do lote é processada; a que falhou é reprocessada sem rodar o handler de novo); `DomainEvent.OccurredOnUtc` vindo do `TimeProvider` (preenchido pela entidade ou pelo interceptor).
- Teste de política (CiPolicy): jobs cuja suíte inclui testes que chamam a Aspire CLI instalam `Aspire.Cli` fixada antes do passo.
- `.gitignore`: ignorar `aspire.config.json` (estado local da CLI do Aspire).
- `ArchitectureRules.OnlyReferences` e `tests/README.md` alinhados ao ADR-0001 (`<Outro>.IntegrationEvents`), com teste próprio.
- `CommitMessageRuleTests.cs`: comentário de resumo com a regra atual (spec citada em qualquer parte da mensagem ou `[no-spec]` no assunto).
- `tests/README.md`: comando `test_commands_contract` literal; comentário do contrato atualizado para SPEC-0005@4.
- Teste de política: filtros de `test`, `test_integration` e o padrão do `coverage.sh` não selecionam `Kind=CommandContract`; nenhum job do CI roda `test_commands_contract`; o UT-02 da SPEC-0006 passa a incluir essa chave.
- `CODEOWNERS` com `tools/sdd/**`, `docs/specs/sdd-config.yml` e `.github/workflows/**` exigindo o dono do repositório.
- Não muda: os contratos das SPEC-0004/0005/0006 (comandos do `sdd-config` iguais), os jobs do CI e a política de auditoria.

**Padrão seguido:** `tests/Subsolo.Tooling.Tests` (CiPolicy e testes de ferramentas) e os scripts existentes em `tools/`.

**Rollback:** revert do merge commit.

**Outras ocorrências:** o teste próprio do `ArchitectureRules.OnlyReferences` fica nesta spec (IT-06, Emenda 1). O tempo do comando `test` (~4 min por causa dos testes de contrato aninhados) fica como questão para decidir na SPEC-0007, quando a suíte crescer.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — área já coberta pelos testes das SPEC-0004/0005/0006.
- **UT-01** — Dado o `ci.yml`, quando a política de CI é avaliada, então o grupo de concorrência usa `github.sha` fora de PR e os caches do pnpm de `web/` e `e2e/` têm chaves distintas, cada uma com o hash do próprio lockfile (falha antes da correção).
- **IT-01** — Dado o servidor estático do E2E rodando, quando recebe `GET /%E0%A4%A`, então responde 400 e continua respondendo 200 numa página existente (`/harness/acessivel.html`; `/` não tem `index.html`) (falha antes: o processo cai).
- **IT-02** — Dado um repositório sem a base `origin/main`, quando `tools/coverage.sh` roda, então termina com exit ≠ 0 e a mensagem da base em poucos segundos, sem rodar a suíte (falha antes da correção).
- **IT-03** — Dado o `tools/coverage.sh`, quando analisado, então não usa `mapfile`/`readarray` e roda num shell bash 3.2 compatível (checagem estática; falha antes da correção).
- **IT-04** — Dado um arquivo `.ts` em `e2e/` com erro de tipo, quando `tools/lint.sh` roda, então o passo `e2e-lint` falha (falha antes da correção).
- **UT-02** — Dados o sdd-config, o `coverage.sh` e o `ci.yml`, quando a política é avaliada, então nenhum filtro de `test`, `test_integration` ou do coverage seleciona `Kind=CommandContract` e nenhum job roda `test_commands_contract` (guarda: passa hoje, falha se alguém reintroduzir).
- **UT-03** — Dado o repositório, quando o `CODEOWNERS` é lido, então `tools/sdd/**`, `docs/specs/sdd-config.yml` e `.github/workflows/**` têm dono (guarda: o `.github/CODEOWNERS` atual já cobre; falha se a regra sumir — Emenda 1).
- **UT-04** — Dado o `ci.yml`, quando a política é avaliada, então todo job cuja suíte inclui testes que chamam a Aspire CLI (`test`, `integration`) instala `Aspire.Cli` na versão fixada antes do passo de testes (guarda — Emenda 1).
- **UT-05** — Dado um `TimeProvider` falso fixado num instante, quando uma entidade levanta um evento de domínio e ele é gravado na outbox, então `OccurredOnUtc` é o instante do `TimeProvider`, não o relógio do sistema (falha antes da correção — Emenda 1).
- **IT-06** — Dado um módulo de exemplo que referencia `<Outro>.Contracts` e outro que referencia `<Outro>.IntegrationEvents`, quando a regra `OnlyReferences` roda, então o primeiro reprova e o segundo passa, conforme o ADR-0001 (falha antes da correção — Emenda 1).
- **IT-07** — Dado o `ProcessMessagesJob` com um `SaveChangesInterceptor` que falha uma vez ao gravar o estado de uma mensagem, quando o lote roda, então a próxima mensagem do lote é processada e a que falhou é reprocessada depois sem rodar o handler de novo (falha antes se o caminho não for tratado; guarda se já for — Emenda 1).
- IT-05 da SPEC-0006 fortalecido (anotação genérica não basta) e a confirmação do CodeQL rodam sob demanda com `SDD_GITHUB_VERIFY=1`, como as demais verificações no GitHub.
- Itens sem teste automatizado (Emenda 1): a confirmação da supressão do CodeQL (item 8) é verificação manual no GitHub registrada no Relatório de Entrega; itens 9, 13 e 15 são documentação/comentários; o item 16 (`aspire.config.json` no `.gitignore`) já estava resolvido.

## 5. Questões em Aberto
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Regressão e guardas**
- [x] Red: UT-01, UT-03, IT-01..IT-04 falhando pelo motivo certo; UT-02 escrito como guarda (passa hoje), todos com a tag `SPEC-0013:<ID>`; ajustar a asserção do IT-05 da SPEC-0006 (anotação genérica não basta)
- [x] Green: `coverage.sh` compatível com bash 3.2 e checagem da base no início; `static-server.mjs` com 400 para URL malformada; lint de `e2e/`; concorrência e chaves de cache no `ci.yml`; CODEOWNERS; README e comentários
- [x] Validar: build + suíte + lint + arquitetura (G2/G3)
- [x] Rodar o IT-05 fortalecido e confirmar a supressão do CodeQL com `SDD_GITHUB_VERIFY=1` (fase 2, depois do G4)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI (G5) e aprovação (H2) — CI verde no PR #18 (run 39018496969)
- [x] Deploy: N/A até o go-live (SPEC-0003)
- [x] Relatório de Entrega e CHANGELOG (G7) — CHANGELOG atualizado

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1ed3cf7 | 2026-10-14 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 2b3ccf3 | 2026-10-17 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — ab4775d | 2026-10-17 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 144ms) — ab4775d | 2026-10-17 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em ab4775d — itens 1–20 atendidos (8 manual pós-G4; 12 e 16 já resolvidos; 9, 13, 15 documentação); guardas UT-02/UT-03/UT-04/IT-07 realmente guardam; IT-05 da SPEC-0006 fortalecido; verify PASS com aviso justificado (correções do harness em e2e/ e ArchitectureRules.cs, comentários). Menores: caches do pnpm com o mesmo store (união), UT-05 sem conferir o Id no conteúdo, falha do dotnet sln list dentro de process substitution — ab4775d | 2026-10-17 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 7m 04s 342ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 1s 011ms); nota: PR #18, CI run https://github.com/thomasravache/subsolo/actions/runs/39018496969 — 10/10 checks verdes em 42f17a9; IT-05 fortalecido da SPEC-0006 verificado no GitHub (SDD_GITHUB_VERIFY=1, PR descartável #19, 1/1 passed) — 42f17a9 | 2026-10-17 |
| H2 Integração aprovada | PASS | aprovado por thomas (delegado: piloto automático, 2026-10-16) | 2026-10-17 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live → SPEC-0003 | 2026-10-17 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 65d1f4b (árvore suja) | 2026-10-17 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
Os 20 ajustes apontados nas revisões das SPEC-0005, 0006, 0007, 0008 e 0012:
- `tools/coverage.sh` compatível com bash 3.2 (sem `mapfile`, arrays vazios protegidos) e conferindo a base `origin/main` antes de rodar a suíte.
- `e2e/static-server.mjs` responde 400 a URL malformada em vez de cair.
- `e2e/` com TypeScript (`tsconfig.json`, `tsc --noEmit`) e o passo `e2e-lint` no `tools/lint.sh`.
- CI: grupo de concorrência por PR ou por commit, um cache do pnpm por lockfile (`web`/`e2e`), job `test` com 35 min (suíte serial + cobertura).
- IT-05 da SPEC-0006 fortalecido: anotação genérica do workflow não basta.
- `ArchitectureRules.OnlyReferences` alinhado ao ADR-0001 (`<Outro>.IntegrationEvents`).
- `DomainEvent.OccurredOnUtc` carimbado pelo `TimeProvider` no interceptor da outbox.
- Guardas: CODEOWNERS dos arquivos do processo, Aspire CLI fixada nos jobs, nenhum filtro seleciona `Kind=CommandContract`, falha ao gravar o estado da mensagem tratada.
- Documentação: `tests/README.md` com os comandos literais do sdd-config e a regra de `IntegrationEvents`; comentários atualizados.

### Como foi feito
- TDD com papéis separados: Test-writer (12fc286, 828eecc, 2b3ccf3) → Implementer (3b9987e, 0a7a65d, c85d5f3, 29f8449, ab4775d, b0de83a) → Reviewer APPROVED.
- Emendas: 1 (plano atualizado depois das ondas 4 e 5 — UT-03 como guarda, novos UT-04, UT-05, IT-06, IT-07, item 16 já resolvido) e 2 (`InsertOutboxMessagesInterceptor.cs` nos `touches`).
- Item 8 (CodeQL): verificado num PR descartável (#20, fechado sem merge) — o comentário `codeql[cs/sql-injection]` não suprime o alerta neste pipeline; o `ci.yml` passou a documentar o caminho real (`query-filters` num config do CodeQL) no commit 65d1f4b.
- Item 7: o IT-05 fortalecido rodou no GitHub com `SDD_GITHUB_VERIFY=1` (PR descartável #19, 1/1 passed).
- Itens 9, 13 e 15: documentação e comentários; itens 12 e 16 já estavam resolvidos (UT-03 e o `.gitignore` como guarda).

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
Red em 2b3ccf3 (`gate G1 --run --at 2b3ccf3`, exit ≠ 0): UT-01 (concorrência sem `github.sha`, cache único), UT-05 (`OccurredOnUtc` com o relógio do sistema), IT-01 (`URIError: URI malformed` derrubando o servidor), IT-02 (suíte rodando sem a base), IT-03 (`mapfile` e array sem proteção), IT-04 (`lint: ok` com erro de tipo em `e2e/`), IT-06 (`Contracts` aceito, `IntegrationEvents` reprovado). Green em ab4775d (`gate G2 --run`) e no CI do PR #18. UT-02, UT-03, UT-04 e IT-07 são guardas: passavam antes e falham se a proteção sumir (provado em rascunho revertido).

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | concorrência por PR/commit e um cache do pnpm por lockfile | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| UT-02 | nenhum filtro seleciona `Kind=CommandContract`; nenhum job roda `test_commands_contract` | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| UT-03 | CODEOWNERS cobre `tools/sdd/**`, `sdd-config.yml` e os workflows | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| UT-04 | jobs `test` e `integration` instalam a Aspire CLI fixada antes dos testes | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| UT-05 | `OccurredOnUtc` vem do `TimeProvider` | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-01 | URL malformada → 400 e o servidor continua de pé | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-02 | sem a base, `coverage.sh` falha rápido com a mensagem da base | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-03 | `coverage.sh` sem recursos do bash 4+ | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-04 | erro de tipo em `e2e/` reprova o `e2e-lint` | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-05 | anotação genérica do workflow não basta (IT-05 da SPEC-0006 fortalecido) | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks); `SDD_GITHUB_VERIFY=1` com PR descartável #19 (1/1 passed) |
| IT-06 | `OnlyReferences` reprova `Contracts` e aceita `IntegrationEvents` | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |
| IT-07 | falha ao gravar o estado: próxima mensagem processada, a que falhou reprocessada sem repetir o handler | PASS | `gate G2 --run` em ab4775d; `gate G5 --run` na integração (42f17a9); CI do PR #18 (run 39018496969, 10/10 checks) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — merge do PR #18 (2fc5efa); G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG atualizado
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003). Entrega na `main` pelo PR #18 (https://github.com/thomasravache/subsolo/pull/18), merge commit 2fc5efa em 2026-10-17, com H2 delegado pelo Thomas (autorização de piloto automático) e merge pelo Architect (ADR-0007).

### Pendências
- Achados menores da revisão, sem spec própria (entram na próxima mudança da área): os dois caches do pnpm salvam a união dos pacotes (store comum); o UT-05 não confere o `Id` do conteúdo; falha do `dotnet sln list` dentro da substituição de processo do `coverage.sh` cai em "nenhum projeto".
- Épico SPEC-0002: o tempo do job `test` no CI (~19 min com a suíte serial) passou da meta de 15 min do épico da fundação — rodar em paralelo os módulos que não sobem o AppHost.
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (escopo) | 2026-10-17 | Emenda 2 (Implementer): entra nos `touches` `src/Common/Subsolo.Common.Infrastructure/Outbox/InsertOutboxMessagesInterceptor.cs`; o UT-05 é atendido pelo interceptor, que já tem o `TimeProvider` e serializa o evento: `domainEvent is DomainEvent d ? d with { OccurredOnUtc = <agora do TimeProvider> } : domainEvent`, preservando o `Id`; o valor padrão de `DomainEvent.OccurredOnUtc` fica só como fallback | O conteúdo do evento é serializado no interceptor, fora dos `touches` | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 1 (escopo e testes) | 2026-10-17 | Emenda 1 (relatório do Test-writer): UT-03 vira guarda (o `.github/CODEOWNERS` já cobre `tools/sdd/**`, `sdd-config.yml` e os workflows); item 16 já resolvido; ganham ID os itens que só estavam na Mudança Proposta — UT-04 (Aspire CLI fixada nos jobs, item 20), UT-05 (`OccurredOnUtc` pelo `TimeProvider`, item 19), IT-06 (`OnlyReferences` com `IntegrationEvents`, item 17, que deixa de ir para a SPEC-0007) e IT-07 (falha ao gravar o estado no `ProcessMessagesJob`, item 18); IT-01 confere uma página existente em vez de `/`; CodeQL (item 8) como verificação manual; chaves de cache `web`/`e2e` por lockfile, com dois passos de cache no job que usa os dois | Spec aprovada antes das ondas 4 e 5; itens sem ID no plano | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
