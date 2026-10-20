---
id: SPEC-0028
title: Páginas Apoie (Pix), privacidade e termos
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0017]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Site/**, tests/Modules/Site/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, Subsolo.slnx, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/apoie/**, web/src/app/(public)/privacidade/**, web/src/app/(public)/termos/**, web/src/app/admin/**, web/src/components/admin/**, web/src/components/site/**, web/src/proxy.ts, web/src/proxy.test.ts, web/package.json, web/pnpm-lock.yaml, tools/compose-up.sh, e2e/tests/site/**, docs/runbooks/**, docs/design/**]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0013, ADR-0017]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0028 — Páginas Apoie (Pix), privacidade e termos

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Três páginas institucionais: "Apoie o Subsolo" (`/apoie`), com texto, QR code e chave Pix para apoio de qualquer valor e a lista de apoiadores; "Privacidade" (`/privacidade`), com o que o Subsolo guarda, para quê, por quanto tempo, os direitos do titular e o canal de contato; e "Termos de uso" (`/termos`). O admin mantém a lista de apoiadores num painel simples. Um runbook curto de resposta a incidente de dados fecha o "Radar LGPD" do épico.

## 2. Motivação & Escopo
**Motivação:** critério de aceite "Nenhum anúncio na agenda; a página Apoie fica acessível pelo rodapé" e decisão do Thomas (2026-10-13): página estática com chave/QR Pix e lista de apoiadores, sem integração de pagamento. LGPD: política de privacidade e canal do titular antes do lançamento (ADR-0008; Radar LGPD do épico).

**Objetivos (dentro do escopo):**
- Módulo pequeno `Site` (conteúdo institucional; ADR-0001 esclarecido) com `Supporter` (nome 2–60, link `https` opcional, ordem de cadastro): `GET /api/v1/supporters` público; `POST /api/v1/admin/supporters` e `DELETE /api/v1/admin/supporters/{id}` só admin.
- `/apoie` (SSR): texto, QR code Pix gerado no servidor a partir da chave (BR Code "copia e cola" sem valor, com o nome e a cidade do recebedor) e "Copiar chave"; a chave, o nome e a cidade vêm da configuração `Support:PixKey`, `Support:PixName`, `Support:PixCity` (no servidor web, lidas das variáveis de ambiente `Support__PixKey`, `Support__PixName`, `Support__PixCity` e `Site__ContactEmail`, só do servidor e nunca `NEXT_PUBLIC_`; definidas no go-live; sem chave, a página mostra "O Pix do Subsolo chega em breve."); lista "APOIADORES" (vazia → "Seja o primeiro a apoiar.").
- `/privacidade` e `/termos` (SSR estáticas): textos do §6 com data de atualização, índice com âncoras e o canal de contato do titular (`Site:ContactEmail`, definido no go-live).
- `/admin/apoiadores`: listar, adicionar e remover (com confirmação).
- `docs/runbooks/incidente-dados.md`: o que fazer num incidente com dados pessoais (conter, avaliar, comunicar a ANPD e os titulares em até 3 dias úteis quando houver risco relevante, registrar).

**Não-objetivos (fora do escopo):**
- Integração de pagamento, valores sugeridos ou confirmação de doação.
- Revisão jurídica dos textos (recomendada ao Thomas antes do go-live).
- Banner de cookies: o Subsolo usa só cookies necessários (sessão, anti-forgery), sem rastreamento.

## 3. Dependências
- **Implementações necessárias:** SPEC-0017 (área `/admin` e proxy das páginas de admin).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** biblioteca de QR code para o servidor do web: `qrcode` (npm, MIT; versão fixada e verificada na implementação, ADR-0009).

## 4. Decisão Arquitetural
**Contexto:** páginas SSR com proxy (ADR-0016), CSP com nonce (ADR-0017), área admin (SPEC-0017), módulos novos no padrão dos existentes; `apoie`, `privacidade` e `termos` já são slugs reservados (SPEC-0017).

**Decisão:** módulo `Site` com os slices de apoiadores; QR code gerado no servidor do Next como SVG (sem script no navegador, compatível com a CSP); textos das páginas como componentes do web, versionados no repositório; configuração do Pix e do contato lida só no servidor (nenhuma variável de ambiente no navegador, SPEC-0016).

**Justificativa:** ADR-0001 (módulo por contexto — o `Site` é esclarecido no ADR), ADR-0008 (política, canal do titular, retenção), ADR-0013 (SSR).

**Desvio do padrão existente:** Nenhum além do esclarecimento do ADR-0001 (novo módulo `Site`).

**Alternativas descartadas:** apoiadores fixos no código (o Thomas escolheu o admin); QR code como imagem estática (não acompanha a chave da configuração); integração com gateway (decisão do épico: sem pagamento no MVP).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0013, ADR-0017

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** páginas estáticas ou quase; `/apoie` com a lista em ≤ 100 ms p95 na API (IT-01).
- **Segurança:** rotas de admin com `admin` + `X-CSRF`; link do apoiador só `https`; textos escapados; QR code em SVG gerado no servidor (sem script inline novo).
- **Privacidade e dados pessoais:** a política descreve o ADR-0008 (dados, finalidades, bases legais, retenção, backups em até 6 meses, direitos e canal); apoiadores são nomes públicos dados por eles mesmos.
- **Disponibilidade e resiliência:** API fora → `/apoie` mostra o Pix sem a lista ("A lista de apoiadores volta já.").
- **Acessibilidade (UI):** axe nas três páginas e no admin; QR code com texto alternativo e a chave sempre em texto; índice com âncoras; "Chave copiada." anunciado.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `GET /api/v1/supporters` · `/api/v1/admin/supporters` · páginas `/apoie`, `/privacidade`, `/termos`, `/admin/apoiadores`

```yaml
GET /api/v1/supporters:                   # AllowAnonymous + default ; 200 [{ id, name, url|null }] (ordem de cadastro)
POST /api/v1/admin/supporters:            # admin + X-CSRF ; body { name: string(2–60), url: https|null } ; 201 { id } ; 400 site.invalid_supporter
DELETE /api/v1/admin/supporters/{id}:     # admin + X-CSRF ; 204 ; 404 site.supporter_not_found
# 401 / 403 / 400 csrf / 429 como no resto da API
```

```text
/apoie: h1 "APOIE O SUBSOLO" ; "O Subsolo é feito pela cena e não tem anúncios. Se ele te ajuda a achar shows, apoie com um Pix de qualquer valor."
  com Support:PixKey → QR code (SVG, alt "QR code do Pix do Subsolo") + chave em texto + "Copiar chave" ("Chave copiada.") ; sem → "O Pix do Subsolo chega em breve."
  "APOIADORES": nomes (link quando houver) ; vazio → "Seja o primeiro a apoiar." ; erro → "A lista de apoiadores volta já."
/privacidade: h1 "PRIVACIDADE" ; "Atualizada em {data}" ; índice ; seções "Que dados guardamos", "Para quê", "Por quanto tempo", "Seus direitos", "Contato"
  (conteúdo: tabela do ADR-0008; sem anúncios nem rastreamento; backups somem em até 6 meses; exportar e excluir em Minha conta; contato Site:ContactEmail)
/termos: h1 "TERMOS DE USO" ; seções "O que é o Subsolo", "Conta", "Quem cadastra shows", "Conteúdo proibido" (discurso de ódio, NSBM, show falso), "Moderação", "Mudanças"
/admin/apoiadores: lista com "Remover" (confirmação) ; "NOVO APOIADOR": Nome, Link (opcional), "Adicionar"
```

**Design:** Stitch, projeto 15194126668426659178: "Apoie o Subsolo v2" (eac502a950a545e481a18f8ea78d4149), "Privacidade - Subsolo" (e5fe5036928f4a29bf3c95fd51132ea3, também modelo de `/termos`) e "Admin - Apoiadores" (af1b0081f220444c893e1d85e071c1bc). Ajustes: QR code gerado da chave real (o Stitch usa uma imagem de exemplo); chave de exemplo `pix@subsolo.rocks` não vai para o código; textos do §6.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ver Apoie com Pix | chave configurada | QR code válido da chave (BR Code com CRC), chave em texto, copiar | UT-01, E2E-01 |
| Sem chave | configuração vazia | "O Pix do Subsolo chega em breve." | UT-01, E2E-02 |
| Apoiadores | lista com e sem itens; API fora | nomes e links; vazio; aviso de erro | IT-01, IT-03, E2E-01 |
| Admin mantém lista | admin adiciona e remove | 201/204; aparece e some em `/apoie` | UT-02, IT-02, E2E-01 |
| Validação | nome curto, link `http` | 400 `site.invalid_supporter` | UT-02, IT-02 |
| Acesso | anônimo / fã nas rotas de admin | 401 / 403 | IT-02 |
| Privacidade e termos | páginas | conteúdo do §6, índice, contato | E2E-02 |
| Rodapé | link "Apoie" e "Privacidade" do rodapé | levam às páginas (200) | E2E-02 |
| Contrato | OpenAPI | endpoints e códigos do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — páginas e módulo novos.

### 7.2 Testes Unitários
- **UT-01** — Dados chave `exemplo@subsolo.test`, nome "Subsolo" e cidade "Curitiba", quando o BR Code é montado, então segue o formato EMV do Pix (campos 00, 26 com o GUI `br.gov.bcb.pix` e a chave, 52, 53=986, 58=BR, 59, 60, 62, 63) com CRC16-CCITT correto (validado contra um payload de referência); sem chave, a página escolhe o texto "chega em breve".
- **UT-02** — Dado o validador de apoiador, quando recebe nome de 1 e 61 caracteres, link `http://x` e `https://zine.example`, então só o último par é aceito.

### 7.3 Testes de Integração
- **IT-01** — Dados a API e o PostgreSQL, quando o admin cadastra dois apoiadores, então `GET /api/v1/supporters` traz os dois na ordem de cadastro em ≤ 100 ms p95.
- **IT-02** — Dados anônimo, fã e admin, quando chamam POST/DELETE de admin, então 401, 403 e 201/204; dados inválidos dão 400; remover inexistente dá 404; sem `X-CSRF` dá 400.
- **IT-03** — Dada a API indisponível (simulada), quando `/apoie` é pedida, então 200 com o Pix e "A lista de apoiadores volta já.".

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json`, quando lido, então os endpoints do §6 têm corpos e códigos, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa com uma chave Pix de teste configurada, nos projetos mobile-chrome e mobile-safari: o fã toca em "Apoie" (navegação) e vê o QR code e a chave; "Copiar chave" anuncia "Chave copiada."; o admin adiciona "Putridearth Zine" em `/admin/apoiadores` e o nome aparece em `/apoie`; remove e some. Sem violações do axe. [jornada: agenda]
- **E2E-02** — No projeto mobile-chrome: com o servidor web sem chave Pix (o Next sem API da SPEC-0008 IT-04), `/apoie` mostra "O Pix do Subsolo chega em breve."; na aplicação completa, o link "Privacidade" do rodapé leva a `/privacidade` e o link "Termos" de `/entrar` leva a `/termos` (200), com as seções e o índice; axe nas duas. [jornada: agenda]

### 7.6 Outros
- Runbook `docs/runbooks/incidente-dados.md` revisado no G4.
- Revisão jurídica dos textos: recomendação ao Thomas antes do go-live (SPEC-0003).

**Dublês e dados de teste:** chave Pix de teste só na configuração da pilha de teste; payload BR Code de referência no UT.

**Ambiente de execução:** IT com Testcontainers; E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003), quando entram `Support:PixKey`, `Support:PixName`, `Support:PixCity` e `Site:ContactEmail`.
- **Dados/schema:** schema novo `site` (aditivo).
- **Compatibilidade:** os links do rodapé (`/apoie`, `/privacidade`), hoje 404, passam a responder 200.
- **Observabilidade:** contador de cliques em "Copiar chave" não é coletado (sem rastreamento); logs do admin com ids.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Chave Pix? — Definida no go-live como configuração (`Support:PixKey`, chave aleatória recomendada); no desenvolvimento, chave de teste (Thomas, 2026-10-18)
- [x] Como manter os apoiadores? — O admin cadastra numa tela simples (Thomas, 2026-10-18)
- [x] Onde ficam os apoiadores? — Módulo novo `Site` (conteúdo institucional), registrado no ADR-0001 (recomendação do Architect; piloto automático do Thomas, 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); chave Pix e manutenção dos apoiadores decididas pelo Thomas em 2026-10-18; telas geradas no Stitch com ele acordado.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: BR Code e validação**
- [ ] Red: UT-01, UT-02 com a tag `SPEC-0028:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Módulo e API**
- [ ] Red: IT-01..IT-03, CT-01 falhando pelo motivo certo
- [ ] Green: módulo `Site`, schema, endpoints, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: Páginas e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: `/apoie`, `/privacidade`, `/termos`, `/admin/apoiadores`, runbook

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 4936069 (árvore suja) | 2026-10-18 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0028`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-19 | E2E-02: "sem chave" no servidor web sem API (SPEC-0008 IT-04), pois a pilha completa do E2E tem chave de teste; `/termos` pelo link "Termos" de `/entrar`, mantendo o rodapé do Stitch (Apoie, Instagram, Privacidade); nomes das variáveis de ambiente do servidor web (`Support__PixKey`, `Support__PixName`, `Support__PixCity`, `Site__ContactEmail`). | SPEC_DEFECTs do Test-writer no G1 | — | piloto automático do Thomas (2026-10-17) |
