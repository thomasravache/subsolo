---
id: SPEC-0015
title: Título da página presente na navegação no cliente
tier: lite
type: fix
user_facing: true
status: implemented
created: 2026-10-17
parent: SPEC-0002
depends_on: []
consumes_contract: []
touches: [web/next.config.ts, web/src/next-config.test.ts]
adrs: [ADR-0013]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0015 — Título da página presente na navegação no cliente

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Na navegação no cliente (clique num `next/link`), o Next 16 entrega os metadados da página nova num bloco separado do conteúdo, e o documento fica cerca de 300 ms sem `<title>`. Nesse intervalo, o leitor de tela anuncia uma página sem nome, o histórico do navegador grava a entrada sem título e o axe acusa `document-title` (WCAG 2.4.2). O problema foi achado na SPEC-0011 (Emenda 3): lá, a troca de cidade passou a recarregar a página inteira. Mas o MVP vai ter muitos links internos com título dinâmico (card do show, perfis, navegação inferior).

**Esperado:** em toda navegação no cliente, o documento sempre tem um `<title>` não vazio, e o título da página nova aparece assim que a URL muda.

## 2. Causa Raiz
O Next 16 faz *streaming* dos metadados para quem não é robô: o `<title>` sai depois do conteúdo, num bloco à parte. Robôs de busca e de prévia de link recebem os metadados bloqueantes, conforme a lista `htmlLimitedBots`. O `web/next.config.ts` não configura `htmlLimitedBots`, então vale o padrão, que trata navegadores comuns como não robôs.

## 3. Mudança Proposta
- `htmlLimitedBots: /.*/` no `web/next.config.ts`: os metadados passam a ser bloqueantes para todos os agentes (decisão do Thomas, 2026-10-17, no épico SPEC-0002).
- Se o E2E-01 mostrar que isso corrige o HTML inicial, mas não a navegação no cliente, o Implementer reporta `SPEC_DEFECT`. A spec ganha uma emenda trocando a correção por navegação completa (`<a>` simples) nos links do shell, como já fizemos no `CitySwitcher`. Não há desvio silencioso.
- **Não muda:** textos, status HTTP (ADR-0016), CSP (ADR-0017), o `CitySwitcher` (continua com `window.location.assign`).

**Padrão seguido:** `web/next.config.ts` existente (SPEC-0008/0011); E2E com axe de `e2e/tests/agenda/**` e `e2e/tests/design/**` (SPEC-0011).

**Rollback:** revert do commit.

**Outras ocorrências:** nenhuma hoje. Toda spec do MVP com link interno herda a proteção; o UT-01 serve de guarda, e o E2E entra com a primeira página interna (Emenda 1).

## 4. Plano de Testes (TDD)
- Caracterização: N/A — o defeito é a configuração atual; o UT-01 falha antes da correção.
- **UT-01** — Dado o `web/next.config.ts`, quando importado em `web/src/next-config.test.ts`, então `htmlLimitedBots` casa com um user agent comum de Chrome mobile e de Safari iOS.
- E2E: N/A — Emenda 1: no código atual nenhum `next/link` leva a uma página interna que responda 200 (`/apoie` e `/privacidade` ainda não existem, dão 404 no RSC e o Next recarrega o documento inteiro), então o título vazio não acontece em nenhuma jornada. O E2E do título (MutationObserver + axe `document-title` na navegação no cliente) passa a ser obrigatório na primeira spec que criar uma página interna 200 alcançável por `next/link` (Página do show ou Apoie), registrado no épico SPEC-0002. O UT-01 é o teste de regressão desta spec.

## 5. Questões em Aberto
- [x] Qual solução adotar? — Metadados bloqueantes com `htmlLimitedBots` global, provados por teste; se não resolverem a navegação no cliente, navegação completa nos links afetados (Thomas, 2026-10-17)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Título na navegação**
- [x] Red: UT-01 com a tag `SPEC-0015:UT-01`, falhando pelo motivo certo (E2E-01 virou N/A pela Emenda 1)
- [x] Green: `htmlLimitedBots: /.*/` no `next.config.ts`
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte + E2E existentes (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 93aac2d (árvore suja) | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 1 (red: [ELIFECYCLE] Test failed. See above for more details.) — f5a749b | 2026-10-17 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 33d0944 | 2026-10-17 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 154ms) — 33d0944 | 2026-10-17 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 33d0944 — htmlLimitedBots /.*/ conferido no Next 16.4.0 (forma documentada de desligar metadata em streaming; bots, PPR de /[cidade] e nonce da CSP sem regressão); 0 bloqueantes, 0 importantes, 2 menores (comentário diz 'todos os agentes' mas requisição sem User-Agent ainda faz streaming; branch com a spec anterior à Emenda 1, resolvido na integração) — 33d0944 | 2026-10-17 |
| G5 Integração & CI | PASS | MANUAL: mesmo resultado integrado da SPEC-0014: gate G5 --run PASS em fc95fc6 (build, test, test_integration, test_e2e, arch_test); PR #22, CI run https://github.com/thomasravache/subsolo/actions/runs/39061454216 — 10/10 checks verdes em 88cc681 | 2026-10-17 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-17 |
| G6 Deploy | N/A | Sem ambiente implantado: deploy fica para o go-live (SPEC-0003), guiado pelo Thomas; entrega na main pelo PR #22 (merge fc652fe) | 2026-10-17 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 7e800f2 (árvore suja) | 2026-10-17 |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-17 | G1 | spec | E2E-01 não reproduz o título vazio no código atual: os únicos next/link do shell que levam a outra página (/apoie, /privacidade) dão 404 no RSC e o Next cai em navegação completa; nenhuma página interna 200 é alcançável por navegação no cliente | Test-writer: link Apoie nos 3 projetos com marcador de recarga; respostas RSC inspecionadas; fallback MPA do Next lido (fetch-server-response.js) | Architect + Thomas | Emenda 1 — E2E-01 N/A, E2E do título herdado pela primeira spec com página interna 200 (épico SPEC-0002); aprovada pelo Thomas em 2026-10-17 | 2026-10-17 |

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
- Metadados da página (título) entregues de forma bloqueante para todo navegador: `htmlLimitedBots: /.*/` no `next.config.ts`, para a página nunca ficar sem título na navegação no cliente.

### Como foi feito
- TDD: Test-writer (f5a749b) → Implementer (33d0944) → Reviewer APPROVED (conferiu no Next 16.4.0 que é a forma documentada, sem regressão em SEO, PPR de `/[cidade]` e CSP com nonce).
- Emenda 1: o E2E-01 não reproduzia o bug no código atual (os únicos links internos levavam a páginas 404 e o Next recarregava o documento); o E2E do título foi herdado pela primeira spec com página interna 200 (registrado no épico).

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
Red em f5a749b (`gate G1 --run`, exit ≠ 0): UT-01 falhou com `htmlLimitedBots não configurado: expected undefined to be an instance of RegExp`. Green em 33d0944 (`gate G2 --run`) e no CI do PR #22.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | a configuração do Next entrega metadados bloqueantes ao Chrome do Android e ao Safari do iOS | PASS | `gate G1 --run` (red em f5a749b); `gate G2 --run` em 33d0944; `gate G5 --run` na integração (fc95fc6); CI do PR #22 (run 39061454216, 10/10 checks em 88cc681) |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — G6 N/A até a SPEC-0003
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7) — CHANGELOG atualizado
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
Sem ambiente implantado (G6 N/A → SPEC-0003). Entrega na `main` pelo PR #22, merge commit fc652fe em 2026-10-17.

### Pendências
- E2E do título na navegação no cliente: na primeira spec com página interna 200 (épico SPEC-0002, Emenda 1).
- Comentário do `next.config.ts` diz "todos os agentes", mas requisição sem `User-Agent` ainda recebe metadados em streaming (achado menor da revisão; navegadores sempre mandam o cabeçalho).
## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (testes e escopo) | 2026-10-17 | E2E-01 vira N/A; o E2E do título passa para a primeira spec com página interna 200 alcançável por `next/link`; `touches` sem `e2e/**`; UT-01 é o teste de regressão | IMP-01: o bug não é alcançável no código atual (único destino interno dá 404 e o Next faz navegação completa) | SPEC-0002 (critério registrado no épico) | thomas, 2026-10-17 |
