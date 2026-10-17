---
id: SPEC-0011
title: Design system escuro e shell do app
tier: full
type: foundation
user_facing: true
status: implemented
created: 2026-10-13
parent: SPEC-0001
depends_on: [SPEC-0008]
consumes_contract: []
contract_version: 2
touches: [src/Modules/Events/Subsolo.Modules.Events.Application/**, src/Modules/Events/Subsolo.Modules.Events.Presentation/**, src/Modules/Events/Subsolo.Modules.Events.Domain/Cities/**, src/Modules/Events/Subsolo.Modules.Events.Infrastructure/Cities/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, web/src/app/layout.tsx, web/src/app/globals.css, web/src/app/(public)/[cidade]/**, web/src/app/icon.png, web/src/app/favicon.ico, web/public/icons/**, docs/design/**, web/src/styles/**, web/src/components/ui/**, web/src/app/(public)/layout.tsx, web/public/fonts/**, web/src/components/ui/__tests__/**, e2e/tests/design/**]
adrs: [ADR-0013]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0011 — Design system escuro e shell do app

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Define a identidade visual do Subsolo, escura e com a cara da cena, no **Stitch** (design system + telas-base, mobile e desktop), e a traduz em código: tokens de Tailwind 4 (cores, tipografia, espaçamento, raio), componentes base acessíveis e o *shell* do app (cabeçalho com seletor de cidade, rodapé com "Apoie" e redes, navegação móvel). A agenda do walking skeleton passa a usar o shell. Todas as telas do épico SPEC-0002 nascem sobre essa base.

## 2. Motivação & Escopo
**Motivação:** "visual escuro, com a cara da cena" é regra inegociável do PRD; sem um design system desde o começo, cada tela gerada por agente fica diferente, e o contraste no escuro costuma falhar em acessibilidade.

**Objetivos (dentro do escopo):**
- No Stitch: projeto "Subsolo", design system escuro (paleta, tipografia, componentes) e 3 telas-base em mobile e desktop: agenda da cidade (com cards de show e o estado vazio), página de show (estrutura) e shell com navegação. Aprovadas pelo Thomas antes do Green desta spec.
- `DESIGN.md` versionado em `docs/design/` com os tokens e as regras de uso (incluindo como exibir flyers de arte extrema sem cortar).
- Tailwind 4 com tokens em `web/src/styles/tokens.css` (`@theme`), tema escuro como padrão.
- Componentes base em `web/src/components/ui/`: `Button`, `Card`, `Badge` (estilo e situação: cancelado/adiado), `Chip` (filtro), `EmptyState`, `Header`, `Footer`, `CitySwitcher`.
- Shell aplicado à rota `/{cidade}`; o rodapé leva a `/apoie` (página criada no épico SPEC-0002) e às redes do Subsolo.
- Fontes self-hosted (sem chamada a terceiros, por causa da CSP da SPEC-0010).

- Ícones do app (`favicon.ico`/`icon.png` e os do PWA) a partir da identidade do Stitch — o navegador pede `/favicon.ico` em toda página e, sem o arquivo, cai na página de não encontrada (ADR-0016).

**Não-objetivos (fora do escopo):**
- Telas específicas das features (cadastro, perfil, painel) — cada spec do épico SPEC-0002 tem as suas telas no Stitch antes do próprio H1.
- Logo definitivo: nesta spec, wordmark tipográfico "Subsolo" criado no Stitch; um logo desenhado, se vier, entra por spec própria.

## 3. Dependências
- **Implementações necessárias:** SPEC-0008 — a rota `/{cidade}` real recebe o shell e é usada no E2E.
- **Contratos consumidos:** N/A — `ShowSummary` v1 e os estados vazio/404/503 vêm da SPEC-0008, já em `depends_on`; o CT-01 protege o uso do schema.
- **Pré-requisitos externos:** acesso ao Stitch (já conectado à sessão como ferramenta MCP); aprovação visual do Thomas.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0013 (Next.js + Tailwind 4, tokens do Stitch) e a resposta do Thomas sobre o design (Stitch, mobile e desktop).

**Decisão:** Stitch como fonte do design; o código exportado serve de referência visual, mas os componentes são reescritos com os tokens e com acessibilidade (o HTML exportado não é colado direto).

**Justificativa:** mantém fidelidade visual sem herdar HTML sem semântica nem contraste verificado.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** biblioteca de componentes pronta (shadcn/ui, Radix themes) sem identidade própria; colar o HTML do Stitch direto (acessibilidade e manutenção ruins).

**ADRs:** ADR-0013

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** fontes com `font-display: swap` e subset latino; o CSS da página da agenda tem < 30 kB gzip — medido no build (G2).
- **Segurança:** fontes e ícones servidos do próprio domínio (compatível com a CSP da SPEC-0010).
- **Privacidade e dados pessoais:** N/A — sem coleta de dados.
- **Disponibilidade e resiliência:** N/A — apenas apresentação.
- **Acessibilidade (UI):** WCAG 2.2 AA: contraste ≥ 4,5:1 para texto e ≥ 3:1 para componentes no tema escuro, foco visível, alvos de toque ≥ 44 px — UT-01, E2E-01.
- **Custo:** zero; o Stitch já está disponível na sessão, sem custo informado.

## 6. Artefato A — Contrato
**Interface:** `tokens de design + componentes base + shell` (consumidos por todas as telas do épico SPEC-0002)

```text
Tokens (CSS @theme): --color-bg, --color-surface, --color-surface-raised, --color-text, --color-text-muted,
  --color-accent, --color-danger (cancelado), --color-warning (adiado), --color-border, --color-focus,
  --font-display, --font-body, --radius-*, --space-*
Componentes (props principais):
  <Button variant="primary|secondary|ghost" size="md|lg">     alvo ≥ 44px, foco visível
  <Card href flyerSrc? title date venue lineup styles status attendingCount>   ← ShowSummary (SPEC-0008@1)
  <Badge kind="style|cancelled|postponed">  <Chip selected onToggle>  <EmptyState title hint action?>
  <Header city cities onCityChange>  <Footer links={apoie, instagram, privacidade}>
Shell: layout de (public) com Header + main + Footer; navegação inferior no mobile (< 768px)
Estados da agenda: lista de Cards | EmptyState "Nenhum show anunciado ainda em {Cidade}." | 404 | 503
```

**Design:** projeto "Subsolo" no Stitch — design system escuro + telas "Agenda da cidade", "Página do show (estrutura)" e "Shell", em mobile e desktop; links e capturas registrados em `docs/design/DESIGN.md` após a aprovação do Thomas.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Contraste dos tokens | pares texto/fundo do tema | todos ≥ 4,5:1 (texto) e ≥ 3:1 (componentes) | UT-01 |
| Card de show | `ShowSummary` completo | título, data no fuso da cidade, casa, line-up, estilos, contagem | UT-02, CT-01 |
| Show cancelado ou adiado | `status` = `cancelled` ou `postponed` | badge visível e texto "Cancelado"/"Adiado" | UT-03 |
| Flyer ausente | `flyerThumbUrl=null` | placeholder escuro com o nome do show | UT-02 |
| Troca de cidade | seleciona outra cidade no `CitySwitcher` | navega para `/{outra}` | UT-04, E2E-01 |
| Shell responsivo | viewport < 768 px e ≥ 768 px | navegação inferior no mobile; cabeçalho completo no desktop | IT-01, E2E-01 |
| Acessibilidade | agenda no mobile e no desktop | axe sem violações; foco visível navegando por teclado | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dados os tokens de cor de `tokens.css`, quando um teste calcula o contraste WCAG dos pares definidos em `DESIGN.md`, então todos atingem ≥ 4,5:1 para texto e ≥ 3:1 para componentes.
- **UT-02** — Dado um `ShowSummary` completo (e outro sem flyer), quando o `Card` é renderizado, então mostra o título, a data e a hora no fuso da cidade, a casa, a line-up, os estilos e a contagem de "vou"; sem flyer, mostra o placeholder com o nome do show.
- **UT-03** — Dado um show com `status=cancelled` (e outro `postponed`), quando o `Card` é renderizado, então o `Badge` "Cancelado" (ou "Adiado") aparece com o papel acessível de status.
- **UT-04** — Dado o `CitySwitcher` com duas cidades, quando o usuário escolhe a outra, então `onCityChange` é chamado com o slug escolhido.

### 7.3 Testes de Integração
- **IT-02** — Dadas as duas cidades seed num PostgreSQL real, quando `GET /api/v1/cities` é chamado (público, `AllowAnonymous`), então a resposta é 200 com `[{ slug, name }]` ordenada por nome (Emenda 2).
- **IT-01** — Dado o build de produção do Next.js, quando `/cidade-exemplo` é renderizada no servidor, então o HTML contém o shell (`header`, `main`, `footer` com o link "Apoie") e o CSS da página fica abaixo de 30 kB gzip.

### 7.4 Testes de Contrato
- **CT-01** — Dado um fixture JSON validado contra o schema `ShowSummary` do OpenAPI (SPEC-0008@1), quando renderizado pelo `Card`, então nenhum campo obrigatório do contrato deixa de ser exibido; o teste falha se o tipo gerado mudar sem que o componente seja ajustado.

### 7.5 Testes E2E
- **E2E-01** — Dado o app subido pelo AppHost com duas cidades seed, quando o fã abre `/cidade-exemplo` no mobile e no desktop, troca de cidade pelo seletor e navega por teclado, então vê o shell escuro, a agenda da nova cidade, o foco sempre visível e nenhuma violação do axe [jornada: agenda].

### 7.6 Outros
- Acessibilidade: axe em mobile e desktop (E2E-01); contraste calculado (UT-01).
- Revisão visual: capturas do Playwright comparadas com as telas aprovadas no Stitch, anexadas ao G4 (revisão humana, não automática).

**Dublês e dados de teste:** fixtures de `ShowSummary` (com e sem flyer, cancelado, adiado) validadas contra o schema do OpenAPI; duas cidades seed.

**Ambiente de execução:** Vitest local e no CI; E2E contra o AppHost local e no CI.

## 8. Plano de Rollout
- **Estratégia:** deploy direto (sem produção até a SPEC-0003).
- **Dados/schema:** N/A
- **Compatibilidade:** consome `ShowSummary` v1 sem alterá-lo.
- **Observabilidade:** N/A — apenas apresentação.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] O design segue alguma identidade existente? — Não: o Subsolo nasce com identidade própria no Stitch; a marca do Thomas aparece como primeira apoiadora no rodapé e na página Apoie (Thomas, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 0: Design no Stitch (aprovação do Thomas)**
- [x] Projeto "Subsolo — Agenda de shows underground" no Stitch (id 15194126668426659178, https://stitch.withgoogle.com/projects/15194126668426659178) com o design system "Subsolo Dark" (asset 16563151862933382783: fundo #0B0B0C, texto #E8E2D4, destaque #D7263D, adiado #F2A93B, cancelado #FF5A5F; Anton/Inter/Space Grotesk; cantos 4px) e as telas Agenda da cidade e Página do show em mobile e desktop, com o shell (cabeçalho, navegação inferior no mobile, rodapé com Apoie) — adiantado em paralelo à onda 2, a pedido do Thomas
- [x] Thomas aprovou as telas em 2026-10-13; o registro dos links e capturas em `docs/design/DESIGN.md` é feito pelo Implementer na Fase 1 (onda 5)

**Fase 1: Tokens e componentes**
- [x] Red: UT-01..UT-04 e CT-01 com a tag `SPEC-0011:<ID>`, falhando pelo motivo certo
- [x] Green: `tokens.css` (Tailwind 4 `@theme`), fontes self-hosted, componentes `Button`, `Card`, `Badge`, `Chip`, `EmptyState`, `Header`, `Footer`, `CitySwitcher`
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte (G2)

**Fase 2: Shell e jornada**
- [x] Red: IT-01 e E2E-01 falhando
- [x] Green: layout `(public)` com o shell aplicado à agenda; navegação inferior no mobile
- [x] Validar: build + suíte + E2E; capturas comparadas com o Stitch anexadas ao G4

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003; validação local pelo Compose quando aplicável
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — ad885b4 | 2026-10-16 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ next typegen && tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 1e50631 | 2026-10-16 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 1s 080ms) — 1e50631 | 2026-10-16 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED em 1e50631 — verify PASS (aviso justificado: 24d7792 só implementa ListAsync no repositório falso; scaffold 1a4c7e7 sem lógica); contrato, contraste, foco, fontes self-hosted e CSP sem terceiros conformes; 0 bloqueantes, 0 importantes, 7 menores (safe-area da BottomNav, keys duplicáveis no Card, RangeError com fuso inválido, CitySwitcher sem JS, Footer no bundle do cliente, collation pt-BR, fontes duplicadas) → épico SPEC-0002 — 1e50631 | 2026-10-16 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 6m 51s 227ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 917ms); nota: PR #16, CI run https://github.com/thomasravache/subsolo/actions/runs/39005176528 — 10/10 checks verdes; bateria local completa verde em b5bebf7 (build, lint, arch, test, integration, e2e, coverage, commands_contract) — 778f2c7 (árvore suja) | 2026-10-17 |
| H2 Integração aprovada | PASS | aprovado por thomas (delegado: piloto automático, 2026-10-16) | 2026-10-17 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live → SPEC-0003 | 2026-10-17 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 778f2c7 (árvore suja) | 2026-10-17 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
A identidade visual "Subsolo Dark" e o shell do app:
- Design system no Stitch e `docs/design/DESIGN.md`; tokens do Tailwind 4 em `web/src/styles/tokens.css` (tema escuro, contraste WCAG garantido por teste); fontes self-hosted (Anton, Inter, Space Grotesk) com `display: swap`.
- Componentes base em `web/src/components/ui/`: `Button`, `Card` (data no fuso da cidade, flyer sem cortar, placeholder), `Badge` (cancelado/adiado), `Chip`, `EmptyState`, `Header`, `Footer`, `BottomNav`, `CitySwitcher`, `AppShell`.
- Shell aplicado à agenda `/{cidade}`: navegação inferior no celular, cabeçalho completo no desktop, link "Pular para o conteúdo", foco visível.
- Endpoint público `GET /api/v1/cities` (cidades ordenadas por nome) e segunda cidade seed `outra-cidade`; troca de cidade carregando o documento novo (título sempre presente).
- Ícones do app (`favicon.ico`, `icon.png` e PWA).

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: scaffold (1a4c7e7) → Test-writer (f05464a, ad885b4) → Implementer (24d7792, ddfeb65, 904aaa7, 8e54dd9, ea204b4, 033c58c, e9817f8, 85ee6a4) → Reviewer APPROVED.
- Emendas: 1 (caminhos `web/src/`, ícones), 2 (endpoint de cidades, seed `outra-cidade`, `timeZone` no Card), 3 (`ICityRepository` no Domain; troca de cidade por navegação completa por causa do `<title>` atrasado na navegação no cliente do Next 16).
- Integração da onda 5: o `Card` usa `<img>` simples em vez de `next/image`, que injetava estilo inline barrado pela CSP da SPEC-0010 (9f2c3cc).

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type feature.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | contraste WCAG dos pares de tokens | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| UT-02 | Card com título, data no fuso da cidade, casa, line-up e placeholder sem flyer | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| UT-03 | badges "Cancelado" e "Adiado" | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| UT-04 | `CitySwitcher` chama `onCityChange` com o slug | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-01 | HTML do build com o shell e CSS da agenda < 30 kB gzip | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| IT-02 | `GET /api/v1/cities` 200 ordenado por nome, só `slug` e `name` | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| CT-01 | Card renderiza fixture validada contra `ShowSummary` | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |
| E2E-01 | agenda no celular e no desktop, troca de cidade, teclado e axe | PASS | `gate G2 --run` da spec; `gate G5 --run` na integração (b5bebf7); CI do PR #16 (run 39005176528, 10/10 checks) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — bateria local e CI verdes no resultado integrado (PR #16, run 39005176528)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — CSS da agenda < 30 kB gzip (IT-01); contraste e axe (UT-01, E2E-01)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — ambiente-alvo é a `main`: merge do PR #16 (ac71d56); G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — rollback = revert do merge commit
- [x] Documentação raiz e CHANGELOG atualizados (G7) — README e CHANGELOG atualizados
- [x] Pendências registradas como novas specs (ou nenhuma) — apontadas para o épico SPEC-0002 e a SPEC-0003 (ver Pendências)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003). Entrega na `main` pelo PR #16 (https://github.com/thomasravache/subsolo/pull/16), merge commit ac71d56 em 2026-10-17, com H2 delegado pelo Thomas (autorização de piloto automático) e merge pelo Architect (ADR-0007).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- Épico SPEC-0002 (achados menores da revisão): safe-area da `BottomNav`; `key` repetível no line-up do Card; fallback para fuso/data inválidos; `CitySwitcher` sem JavaScript; `Footer` fora do bundle do cliente; collation pt-BR na ordenação de cidades; fontes duplicadas em `public/`; título ausente durante a navegação no cliente (questão em aberto da SPEC-0002).
- SPEC-0003: endereço definitivo do Instagram no rodapé (questão em aberto).
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0011`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 3 | 2026-10-16 | Emenda 3 (relatório do Implementer): entra nos `touches` `src/Modules/Events/Subsolo.Modules.Events.Domain/Cities/**` (o `ListAsync` de `ICityRepository`, que pelo padrão do projeto fica no Domain); a troca de cidade no `CitySwitcher` carrega o documento novo (`window.location.assign`) em vez de navegar no cliente, porque na navegação no cliente o Next 16 entrega o `<title>` em bloco separado e a página fica ~300 ms sem título (axe `document-title`, WCAG 2.4.2). Pendência registrada para o épico SPEC-0002: o mesmo intervalo sem título vale para links internos com título dinâmico (card, navegação inferior); avaliar `htmlLimitedBots` ou correção do Next antes das telas do MVP | E2E-01 intermitente no mobile-chrome; G4 reprovava escopo | nenhuma (mesmos contratos e textos) | thomas (delegado: piloto automático, 2026-10-16, recomendação do Architect) |
| 2 | 2026-10-16 | Emenda 2 (impedimentos do Test-writer): o `CitySwitcher` lista as cidades de um endpoint novo e público `GET /api/v1/cities` → `[{ slug, name }]` ordenado por nome (vertical slice `ListCities` no módulo Events, OpenAPI e cliente regenerados), em vez de uma lista fixa no front (a cidade é dado, sistema multi-cidade); o seed de desenvolvimento ganha a segunda cidade `outra-cidade` ("Outra Cidade", America/Sao_Paulo) usada pelo E2E-01; novo IT-02; o `Card` recebe `timeZone` (IANA) para mostrar a data no fuso da cidade | E2E-01 exigia duas cidades e uma origem para a lista | SPEC-0008 (contrato da API ganha um endpoint; nada muda no existente) | thomas (delegado: piloto automático, 2026-10-16, recomendação do Architect) |
| 1 (escopo) | 2026-10-16 | Emenda 1 (preparação do Architect): o app Next fica em `web/src/app/`; entram nos `touches` o layout raiz, o `globals.css`, a página da agenda (`(public)/[cidade]/**`, para aplicar o shell e os componentes sem mudar textos nem status) e os ícones do app | Estrutura real do projeto (SPEC-0008) | SPEC-0008 (mesmos textos e status; E2E-01 continua valendo) | thomas (delegado: piloto automático, 2026-10-16) |
