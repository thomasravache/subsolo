---
id: SPEC-0035
title: "CI no runner Ubuntu 26.04: runner fixo, cache do Playwright por imagem e imagens Docker"
tier: lite
type: fix
user_facing: false
status: approved
created: 2026-10-23
parent: SPEC-0002
depends_on: []
consumes_contract: []
touches: [.github/workflows/ci.yml, tests/Subsolo.Tooling.Tests/CiPolicy/**]
adrs: [ADR-0005, ADR-0009]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-23
---

# SPEC-0035 — CI no runner Ubuntu 26.04: runner fixo, cache do Playwright por imagem e imagens Docker

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Em 2026-10-23 o `ubuntu-latest` do GitHub Actions passou a ser o Ubuntu 26.04 (imagem `ubuntu-26.04`, versão 20261012.163.1). O primeiro CI do PR #42 nessa imagem (run 40033852786) falhou em dois jobs sem nenhum defeito no código:
- **e2e:** todos os testes do projeto `mobile-safari` falham em 2 ms com `MiniBrowser: error while loading shared libraries: libicudata.so.74`. O WebKit veio do cache `~/.cache/ms-playwright`, baixado num runner 24.04, e o `playwright install --with-deps` não baixa de novo um navegador que já está no cache.
- **integration:** os testes passam, mas o passo "Salva imagens Docker usadas pelos testes" sai com 123: `docker image save` recebe `testcontainers/ryuk:0.14.0`, que aparece no `docker image ls` e já não pode ser salva (`No such image`).

**Impacto:** todo PR fica vermelho, e o merge do PR #42 (SPEC-0030 e SPEC-0034) está parado.

**Esperado:** o CI verde e previsível; uma troca de imagem do runner acontece de propósito, sem quebrar o cache.

## 2. Causa Raiz
- Os jobs usam `runs-on: ubuntu-latest`, que muda de versão sem aviso, ao contrário do restante da stack, fixada em versão exata (ADR-0009).
- A chave do cache dos navegadores do Playwright (`playwright-<os>-<hash do lockfile>`) não considera a imagem do runner. Um navegador compilado para outra versão do Ubuntu é reaproveitado.
- O passo que salva as imagens Docker do cache dos Testcontainers trata a lista do `docker image ls` como garantida e falha o job inteiro por causa de uma otimização.

## 3. Mudança Proposta
- Todos os jobs do `.github/workflows/ci.yml` com `runs-on: ubuntu-24.04`, a imagem em que o CI sempre rodou verde. A ida para o 26.04 fica para uma mudança própria.
- O cache `~/.cache/ms-playwright` ganha na chave a imagem do runner (`ImageOS` e `ImageVersion`, lidas num passo antes do cache). A chave fica `playwright-<os>-<imagem>-<hash do e2e/pnpm-lock.yaml>`.
- O passo "Salva imagens Docker usadas pelos testes" salva só as imagens que o `docker image inspect` ainda encontra e não reprova o job (`continue-on-error: true`), porque o cache é só uma otimização.
- **Não muda:** os comandos do `sdd-config.yml`, os jobs e os nomes dos checks obrigatórios, as versões das ações (SHA fixo) e o código do produto.

**Padrão seguido:** testes de política do workflow em `tests/Subsolo.Tooling.Tests/CiPolicy/` (SPEC-0006 e SPEC-0013), que leem o `ci.yml` com YamlDotNet, como `CiWorkflowConcurrencyAndCacheTests`.

**Rollback:** revert do commit.

**Outras ocorrências:** nenhuma. Não há outro workflow além do `ci.yml`.

## 4. Plano de Testes (TDD)
- Caracterização: N/A. Os testes de política existentes (`CiPolicy`) cobrem o resto do workflow e continuam passando.
- **UT-01** — Dado o `ci.yml`, quando o teste lê o `runs-on` de cada job, então todos usam uma imagem de versão fixa `ubuntu-24.04` e nenhum usa `ubuntu-latest`.
- **UT-02** — Dado o `ci.yml`, quando o teste lê o passo `actions/cache` de `~/.cache/ms-playwright`, então a chave tem o hash do `e2e/pnpm-lock.yaml` e a saída de um passo anterior do mesmo job que lê `ImageOS` e `ImageVersion`.
- **UT-03** — Dado o `ci.yml`, quando o teste lê o passo "Salva imagens Docker usadas pelos testes", então ele tem `continue-on-error: true` e o script filtra a lista com `docker image inspect` antes do `docker image save`.
- Integração/E2E: N/A. O CI do PR #42 é a prova: e2e e integration verdes no runner fixo.

## 5. Questões em Aberto
- [x] Fixar o runner ou só ajustar o cache para o 26.04? — Os dois: fixar `ubuntu-24.04` agora (ADR-0009, versões exatas) e deixar a chave do cache imune a uma futura troca de imagem. Recomendação do Architect, no piloto automático do Thomas (2026-10-23).

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

H1 delegado: piloto automático do Thomas até o deploy (2026-10-23, "pode continuar de onde parou no modo automático com o recomendado até chegarmos na parte do deploy"). Falha de ambiente achada no CI do PR #42.

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Runner fixo e caches**
- [ ] Red: UT-01, UT-02 e UT-03 com a tag `SPEC-0035:<ID>`, falhando pelo motivo certo
- [ ] Green: `runs-on: ubuntu-24.04`, chave do cache do Playwright com a imagem, salvamento de imagens tolerante
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 8ea72b4 | 2026-10-23 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 6eb045d | 2026-10-23 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 2a45f95 | 2026-10-23 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 10s 137ms) — 2a45f95 | 2026-10-23 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (0 blocker/major; 2 minor aceitos: ImageVersion renova o cache do Playwright a cada imagem nova, \|\| true engole falha do docker image ls — o cache é só otimização; 3 nits) — 2a45f95 | 2026-10-23 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 14m 21s 900ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 9s 034ms) — 903c8cd | 2026-10-23 |
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
