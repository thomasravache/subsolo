---
id: SPEC-0017
title: Cidades e estilos no painel do admin
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-17
parent: SPEC-0002
depends_on: [SPEC-0014, SPEC-0016]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Events/**, tests/Modules/Events/**, docs/api/**, web/src/api/**, web/src/app/admin/**, web/src/components/admin/**, e2e/tests/admin/**, docs/design/**, tools/compose-up.sh, web/src/proxy.ts, web/src/proxy.test.ts, web/src/components/ui/shell.ts, web/src/components/ui/__tests__/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/packages.lock.json, tests/**/packages.lock.json]
adrs: [ADR-0001, ADR-0002, ADR-0003, ADR-0013, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0017 — Cidades e estilos no painel do admin

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O admin cadastra e edita, pelo painel, as **cidades** que têm agenda (nome, UF, fuso e endereço `/{cidade}`) e os **estilos** de metal usados nos shows e nos filtros. A lista começa com death, black, grind, doom e thrash, e o admin pode acrescentar outros. Uma cidade nova aparece na hora no seletor de cidade e ganha a agenda dela.

## 2. Motivação & Escopo
**Motivação:** o sistema nasce multi-cidade, e a cidade inicial é um dado cadastrado pelo admin, não código (resposta do Thomas no épico). Os estilos são uma lista editável (idem). Hoje as cidades só existem no seed de desenvolvimento.

**Objetivos (dentro do escopo):**
- Estilos no módulo `Events`: entidade `Style` (slug, nome), com os 5 estilos iniciais criados por **migração de dados** (valem em produção) e `GET /api/v1/styles` público.
- Endpoints de admin (`RequireAuthorization(admin)`): listar, criar e editar cidades; criar e renomear estilos.
- O endereço da cidade (slug) é gerado do nome (sem acento, minúsculas, hífens), pode ser editado na criação e **não muda depois** (links compartilhados não quebram).
- Endereços reservados não podem virar cidade: `entrar`, `conta`, `admin`, `apoie`, `privacidade`, `termos`, `api`, `health`, `_next`, `show`, `banda`, `casa`, `produtor`, `buscar`, `seguindo`, `offline`.
- Tela `/admin/cidades-e-estilos` (desktop primeiro, utilizável no celular). Anônimo é redirecionado para `/entrar?returnTo=…`; quem não é admin recebe uma página 403.

**Não-objetivos (fora do escopo):**
- Apagar cidade ou estilo: um show vai apontar para eles, então só renomear ou editar. Apagar com regra de "sem shows" fica para quando os shows existirem, se for preciso.
- Contagem de shows por cidade ou estilo (entra com o cadastro de show).
- Resto do painel do admin (aprovação de perfis, moderação: specs próprias).

## 3. Dependências
- **Implementações necessárias:** SPEC-0014 — papel `admin` e sessão; SPEC-0016 — o E2E entra como admin pelo link mágico.
- **Contratos consumidos:** N/A — usa a implementação das specs acima.
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** slices do `Events` (`Features/GetCityAgenda`, `Features/ListCities`, SPEC-0008/0011), entidade `City` existente, páginas SSR com o shell (SPEC-0011), cliente OpenAPI, anti-forgery (SPEC-0010), status das páginas pelo proxy (ADR-0016).

**Decisão:** novos slices no `Events`: `CreateCity`, `UpdateCity`, `ListCitiesForAdmin`, `ListStyles`, `CreateStyle`, `RenameStyle`. Os endpoints de admin ficam sob `/api/v1/admin/**`. O `City` ganha os métodos `Update` e a validação de slug reservado. A página do admin chama a API pelo navegador, com o cookie de sessão e o X-CSRF.

**Justificativa:** cidade e estilo são dados da agenda (módulo `Events`, ADR-0001); slug imutável preserva URLs (ADR-0016); seed por migração garante os estilos em qualquer ambiente.

**Desvio do padrão existente:** Nenhum

**Alternativas descartadas:** estilos como enum no código (o Thomas quer a lista editável); módulo separado só para "catálogo" (pouco conteúdo e mais acoplamento); slug editável (quebra links compartilhados).

**ADRs:** ADR-0001, ADR-0002, ADR-0003, ADR-0013, ADR-0016

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** N/A — dezenas de cidades e estilos; nada a medir além da suíte.
- **Segurança:** endpoints de admin com política `admin` (anônimo 401, fã 403, IT-04); mutações com X-CSRF (IT-05); entrada validada (tamanhos, UF entre as 27, fuso IANA válido, slug no padrão e fora dos reservados) (UT-01, UT-02).
- **Privacidade e dados pessoais:** N/A — cidade e estilo são dados públicos.
- **Disponibilidade e resiliência:** N/A — operação administrativa síncrona; erro do banco vira 500 em ProblemDetails (já coberto pela SPEC-0007).
- **Acessibilidade (UI):** WCAG 2.2 AA na tela do admin — axe no E2E-01; formulários com rótulo visível e erros ligados ao campo.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `/api/v1/styles` · `/api/v1/admin/cities` · `/api/v1/admin/styles` · tela `/admin/cidades-e-estilos`

```yaml
GET /api/v1/styles:                         # AllowAnonymous
  200: [{ slug: string, name: string }]     # ordenado por nome

GET /api/v1/admin/cities:                   # RequireAuthorization(admin)
  200: [{ slug, name, state, timeZone }]    # ordenado por nome
POST /api/v1/admin/cities:                  # admin + X-CSRF
  body: { name: string(2–60), state: UF, timeZone: IANA, slug: string|null }   # slug null → gerado do nome
  201: { slug, name, state, timeZone }  (Location: /api/v1/admin/cities/{slug})
  400: ProblemDetails code=events.invalid_city (errors por campo) | events.city_slug_reserved
  409: ProblemDetails code=events.city_slug_taken
PUT /api/v1/admin/cities/{slug}:            # admin + X-CSRF; slug não muda
  body: { name, state, timeZone }
  200: { slug, name, state, timeZone }
  400: events.invalid_city ; 404: events.city_not_found

POST /api/v1/admin/styles:                  # admin + X-CSRF
  body: { name: string(2–30) }              # slug gerado do nome
  201: { slug, name } ; 400: events.invalid_style ; 409: events.style_taken (mesmo slug)
PUT /api/v1/admin/styles/{slug}:            # admin + X-CSRF; slug não muda
  body: { name }
  200: { slug, name } ; 400: events.invalid_style ; 404: events.style_not_found ; 409: events.style_taken (nome igual a outro)

Comuns: 401 auth.required ; 403 auth.forbidden ; 400 security.csrf_invalid
```

```text
Estilos iniciais (migração de dados): death, black, grind, doom, thrash (slug = nome).
Slug: sem acento, minúsculas, espaços → hífen, ^[a-z0-9-]{2,60}$ ; reservados: ver §2.
UF: as 27 siglas. Fuso: qualquer IANA válido; a tela oferece os fusos do Brasil, com America/Sao_Paulo como padrão.

/admin/cidades-e-estilos (SSR) — título "Cidades e estilos | Admin | Subsolo"
  anônimo → 307 para /entrar?returnTo=/admin/cidades-e-estilos ; fã → página "Você não tem acesso a esta página." (status 403)
  Coluna CIDADES: tabela (Nome, UF, Fuso, Endereço /{slug}) ; "Nova cidade" → formulário (Nome, UF, Fuso, Endereço com prévia
    "subsolo.app/{slug}" gerado do nome e editável) ; "Editar" na linha (Endereço só leitura) ; erros no campo
    ("Esse endereço já existe.", "Esse endereço é reservado.")
  Coluna ESTILOS: chips com os estilos ; campo "Novo estilo" + "Adicionar" ; "Renomear" no chip ; erro "Esse estilo já existe."
  Sucesso: mensagem "Cidade criada." / "Estilo adicionado." em região aria-live
```

**Design:** Stitch, projeto "Subsolo — Agenda de shows underground" (15194126668426659178), tela "Cidades e estilos" (desktop), apresentada no H1. As contagens de shows que o Stitch mostrar ficam de fora até o cadastro de show existir. Captura em `docs/design/stitch/` depois do H1.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Estilos iniciais | banco migrado | `GET /styles` traz death, black, doom, grind, thrash | IT-01 |
| Criar cidade | admin, dados válidos, slug nulo | 201 com slug gerado (`São José dos Pinhais` → `sao-jose-dos-pinhais`); aparece em `GET /cities` e `/{slug}` mostra a agenda vazia | UT-01, IT-02, E2E-01 |
| Dados de cidade inválidos | nome curto, UF inexistente, fuso inválido, slug fora do padrão | 400 `events.invalid_city` com o campo | UT-02, IT-02 |
| Slug reservado | `admin`, `entrar`… | 400 `events.city_slug_reserved` | UT-02, E2E-01 |
| Slug repetido | slug já existente | 409 `events.city_slug_taken` | IT-02 |
| Editar cidade | admin, slug existente | 200; slug inalterado | UT-01, IT-03 |
| Cidade inexistente | PUT em slug sem cidade | 404 `events.city_not_found` | IT-03 |
| Criar estilo | nome novo | 201; aparece em `GET /styles` | UT-03, IT-01, E2E-01 |
| Estilo inválido ou repetido | 1 caractere, 31 caracteres, nome que gera slug existente | 400 `events.invalid_style` / 409 `events.style_taken` | UT-03, IT-01 |
| Renomear estilo | admin | 200; slug inalterado; nome igual a outro → 409; slug inexistente → 404 | UT-03, IT-01 |
| Sem permissão | anônimo / fã | 401 / 403 na API; tela: redirect para `/entrar` / página 403 | IT-04, E2E-02 |
| Sem anti-forgery | mutação sem X-CSRF | 400 `security.csrf_invalid` | IT-05 |
| Contrato publicado | snapshot OpenAPI | rotas, corpos e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — `GET /api/v1/cities` e o seletor de cidade seguem cobertos pelos testes da SPEC-0011 (IT-02 e E2E-01 de lá).

### 7.2 Testes Unitários
- **UT-01** — Dado o gerador de slug, quando recebe "São José dos Pinhais", "  Curitiba ", "Foz do Iguaçu!", então gera `sao-jose-dos-pinhais`, `curitiba`, `foz-do-iguacu`; dado `City.Update`, então nome, UF e fuso mudam e o slug não.
- **UT-02** — Dados nome com 1 ou 61 caracteres, UF `XX`, fuso `Mars/Olympus`, slug `Cidade_X` e cada slug reservado do §2, quando `CreateCityCommandValidator` valida, então retorna `events.invalid_city` com o campo ou `events.city_slug_reserved`.
- **UT-03** — Dados nomes de estilo com 1 e 31 caracteres, quando validados, então `events.invalid_style`; dado "Black" com `black` existente, então `events.style_taken`; renomear mantém o slug.

### 7.3 Testes de Integração
- **IT-01** — Dado o PostgreSQL real migrado do zero, quando `GET /api/v1/styles` é chamado, então traz os 5 estilos iniciais em ordem de nome; como admin, `POST /admin/styles` com "Crust" dá 201 e o estilo aparece na lista; repetir dá 409; renomear para "Crust Punk" mantém o slug `crust`; renomear para "Doom" dá 409; renomear um slug inexistente dá 404.
- **IT-02** — Dado um admin, quando cria "São José dos Pinhais"/PR/America/Sao_Paulo, então 201 com slug gerado, `GET /api/v1/cities` a inclui e `GET /api/v1/cities/sao-jose-dos-pinhais/agenda` responde 200; dados inválidos dão 400 com o campo; slug repetido dá 409.
- **IT-03** — Dada uma cidade existente, quando o admin faz PUT com nome e fuso novos, então 200 e o slug continua o mesmo; PUT num slug inexistente dá 404.
- **IT-04** — Dados um anônimo e um fã, quando chamam cada endpoint `/api/v1/admin/**`, então 401 `auth.required` e 403 `auth.forbidden`.
- **IT-05** — Dado um admin sem X-CSRF, quando chama cada mutação, então 400 `security.csrf_invalid` e nada muda.

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então `/api/v1/styles`, `/api/v1/admin/cities` e `/api/v1/admin/styles` têm corpos, status e códigos do §6, e `(cd web && pnpm api:check)` passa com o cliente regenerado.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa, no projeto desktop-chrome: o admin (`admin@subsolo.test` em `Users:AdminEmails` do ambiente de teste) entra pelo link mágico com o `returnTo` do painel, abre `/admin/cidades-e-estilos`, cria "São José dos Pinhais" (vê a prévia do endereço `sao-jose-dos-pinhais`), vê "Cidade criada." e a cidade na tabela; tentar o endereço `admin` mostra "Esse endereço é reservado."; adiciona o estilo "Crust" e o vê entre os chips. Depois, `/sao-jose-dos-pinhais` mostra a agenda vazia e a cidade aparece no seletor de cidade. Sem violações do axe. [jornada: admin-cidades]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome: anônimo em `/admin/cidades-e-estilos` vai para `/entrar?returnTo=/admin/cidades-e-estilos`; um fã logado vê "Você não tem acesso a esta página." com status 403. [jornada: admin-cidades]

### 7.6 Outros
- Acessibilidade: axe na tela do admin e na página 403 (E2E-01 e E2E-02).

**Dublês e dados de teste:** `admin@subsolo.test` como admin só na configuração de teste; cidades de teste com nomes únicos por execução.

**Ambiente de execução:** IT com Testcontainers; E2E contra a pilha do Compose com o Mailpit (helper de login da SPEC-0016); `--workers=1`.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** migração aditiva no schema `events` (tabela `styles`) com os 5 estilos iniciais; o seed de desenvolvimento das cidades continua.
- **Compatibilidade:** `GET /api/v1/cities` não muda; rotas novas em `/api/v1`.
- **Observabilidade:** log de auditoria (id do admin, ação, slug) em cada criação ou edição; sem métricas novas.
- **Rollback:** reverter o merge commit; a tabela de estilos pode ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Cidade e estilo podem ser apagados? — Não no MVP: só criar e editar ou renomear; o slug não muda (recomendação do Architect, 2026-10-17; confirmada no H1)
- [x] Tela "Cidades e estilos" aprovada no Stitch (Thomas) — gerada em 2026-10-17 ("Admin - Cidades e Estilos", a14b19f0df03454483d4dd012d44ea8f, desktop); ajustes propostos: sem textos de exclusão nem a caixa "Regra da cena", sem contagens de shows, menu só com o que existe, endereço só leitura na edição, domínio real no lugar de "subsolo.art". Aprovada com esses ajustes pelo Thomas em 2026-10-17

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); a tela do admin foi aprovada por ele com os ajustes no mesmo dia.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Domínio e regras**
- [ ] Red: UT-01, UT-02, UT-03 com a tag `SPEC-0017:<ID>`, falhando pelo motivo certo
- [ ] Green: implementar o mínimo seguindo o padrão de referência
- [ ] Refactor mantendo tudo verde

**Fase 2: API, persistência e contratos**
- [ ] Red: IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 falhando pelo motivo certo
- [ ] Green: endpoints, migração aditiva, OpenAPI e cliente TS regenerados
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 3: Telas e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: telas conforme o Stitch e os ajustes do §6; jornada completa passando localmente
- [ ] Validar: build + suíte + E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 54ab48c (árvore suja) | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 6fcaceb | 2026-10-18 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — fe0b8cf | 2026-10-18 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 2s 465ms) — fe0b8cf | 2026-10-18 |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0017`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (escopo) | 2026-10-17 | `touches` ganha `tools/compose-up.sh` (admin `admin@subsolo.test` na pilha da E2E por `Users__AdminEmails__0`), `web/src/proxy.ts` e seu teste (status da página do admin: 307 para anônimo, 403 para fã, `no-store` — padrão do ADR-0016), `web/src/components/ui/shell.ts` e seus testes, `src/Subsolo.Api/Program.cs` e os lock files | Relatório do Test-writer: sem admin na pilha da E2E o E2E-01 falharia pelo motivo errado; status de página é responsabilidade do proxy | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
