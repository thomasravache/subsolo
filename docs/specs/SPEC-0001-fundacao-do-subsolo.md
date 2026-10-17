---
id: SPEC-0001
title: Fundação do Subsolo
tier: epic
type: foundation
status: approved
created: 2026-10-13
depends_on: []
adrs: []
external: []
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0001 — Fundação do Subsolo (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0001`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
Deixar o Subsolo pronto para receber features com segurança e rapidez: stack escolhida por critério e aprovada em ADRs, repositório com convenções, testes (unitário, integração com dependências reais, E2E) rodando local e no CI, ambientes de staging e produção como código, e um *walking skeleton* (a jornada mais fina: abrir a agenda da cidade vazia) publicado em produção pelo pipeline, com observabilidade e segurança básicas.

## 2. Escopo
**Objetivos (dentro do escopo):**
- Stack e arquitetura decididas e aprovadas: ADR-0001 a ADR-0015 (todos os pilares do perfil `padrao`).
- Repositório com convenções e o controle de SDD instalado (SPEC-0004).
- Testes de todos os níveis rodando localmente com um comando cada (SPEC-0005) e no CI (SPEC-0006).
- Base do monólito modular e AppHost do Aspire (SPEC-0007).
- Walking skeleton: agenda da cidade ponta a ponta, também pelo Docker Compose gerado pelo Aspire (SPEC-0008).
- Observabilidade, segurança e design system base (SPEC-0009, SPEC-0010, SPEC-0011).

**Não-objetivos (fora do escopo):**
- Qualquer item com custo — VPS em produção, domínio, DNS, e-mail real, backup externo e monitor — fica para o épico de go-live SPEC-0003 (decisão do Thomas, ADR-0012).
- Features do produto (épico SPEC-0002).

## 3. Arquitetura Alvo
**Contexto:** projeto novo; stack e arquitetura definidas nos ADRs de fundação, com escolhas do Thomas (origin: user): .NET 10 em monólito modular com vertical slices (Milan Jovanovic), Aspire, VPS própria, GitHub, login por link mágico + Google, design no Stitch.

```
Navegador (PWA) ──HTTPS──> Caddy ──/api/*──> Subsolo.Api (.NET 10) ──> PostgreSQL 18 (schema por módulo)
                              │                  │  módulos: Users · Profiles · Events · Engagement · Notifications · Moderation
                              │                  │  outbox/inbox + Quartz (eventos entre módulos)
                              └──/*────────────> web (Next.js 16, SSR + PWA)
                              └──/media/*──────> volume de flyers (NetVips)
Telemetria: api + web ──OTLP──> Seq        Local: tudo orquestrado pelo Aspire AppHost (aspire run)
Deploy (go-live): aspire publish → docker-compose.yaml → VPS via GitHub Actions
```

Os contratos entre as filhas são: os comandos do sdd-config (SPEC-0004 → SPEC-0005 → SPEC-0006), os blocos `Common` (SPEC-0007) e o contrato HTTP `/api/v1` + `ShowSummary` (SPEC-0008), consumidos pelas SPEC-0009, SPEC-0010 e SPEC-0011 e por todo o épico SPEC-0002.

**Decisões (ADRs):** ADR-0001 arquitetura · ADR-0002 dados · ADR-0003 identidade · ADR-0004 entrega · ADR-0005 testes · ADR-0006 qualidade · ADR-0007 fluxo de mudança · ADR-0008 segredos e LGPD · ADR-0009 dependências · ADR-0010 observabilidade · ADR-0011 resiliência · ADR-0012 custo · ADR-0013 front-end · ADR-0014 avisos · ADR-0015 flyers.

**Regras de arquitetura a garantir (G3):** Domain não depende de Application/Infrastructure/Presentation; um módulo só referencia `<Outro>.IntegrationEvents`; cada `DbContext` usa só o seu schema; todo endpoint declara a política de autorização; só o `Notifications` referencia push/SMTP; só `Common.Infrastructure` referencia NetVips — todas em `tests/Subsolo.ArchitectureTests` (ArchUnitNET), criadas nas SPEC-0007, SPEC-0010 e nas specs dos módulos.

## 4. Decomposição
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0004 | Repositório e ferramentas | full | foundation | M | — | — |
| SPEC-0005 | Harness de testes | full | foundation | M | SPEC-0004 | — |
| SPEC-0006 | Pipeline de CI no GitHub Actions | full | foundation | S | SPEC-0004 | SPEC-0005@1 |
| SPEC-0007 | Base do monólito modular com Aspire | full | foundation | M | SPEC-0005 | — |
| SPEC-0008 | Walking skeleton: agenda da cidade | full | foundation | M | SPEC-0006, SPEC-0007 | — |
| SPEC-0009 | Observabilidade base | full | foundation | M | SPEC-0008 | — |
| SPEC-0010 | Segurança base | full | foundation | M | SPEC-0008 | — |
| SPEC-0011 | Design system escuro e shell do app | full | foundation | M | SPEC-0008 | — |

## 5. Estratégia de Entrega
- **Ambientes:** apenas local (Aspire `aspire run` + Compose gerado, rodando localmente) e CI no GitHub Actions; staging e produção chegam com o épico SPEC-0003, depois da aprovação de custos.
- **Entrega por onda:** cada onda é integrada na `main` por PR com CI verde e H2; o "deploy" desta fase é a `main` verde + o Compose local passando no smoke (o G6 das filhas é N/A → SPEC-0003).
- **Feature flags:** N/A — nada exposto a usuários reais nesta fase.
- **Rollback:** reverter o merge commit da onda na `main`.
- **Métricas de sucesso pós-release:** tempo do CI ≤ 15 min (p50); a suíte completa verde por 5 PRs seguidos sem teste instável; `aspire run` sobe tudo numa máquina limpa em ≤ 5 min.

## 6. Riscos & Mitigações
- Publicação em Docker Compose do Aspire é recente e pode não cobrir tudo — mitigação: validada já no walking skeleton (SPEC-0008), com ajustes via `PublishAsDockerComposeService`.
- Ir para produção só no fim esconde problemas de ambiente — mitigação: o mesmo Compose e o mesmo Caddy rodam localmente desde a SPEC-0008.
- Complexidade do monólito modular para uma pessoa — mitigação: os blocos `Common` e os testes de arquitetura prontos na SPEC-0007, com um módulo de exemplo.
- Sem branch protection, caso o repositório seja privado no plano Free — mitigação: a recomendação é o repositório público (pergunta na SPEC-0004).
- Bibliotecas que mudam de licença — mitigação: a política de licenças no ADR-0009.

## 7. Critérios de Aceite do Épico
- [ ] Um fã abre a agenda da cidade no celular, renderizada pelo Next.js a partir da API e do banco — SPEC-0008:E2E-01
- [ ] A aplicação sobe pelo Docker Compose gerado pelo Aspire e passa no smoke — SPEC-0008:E2E-02
- [ ] Todo PR roda build, lint, testes, E2E, segurança e o job `sdd`, e um teste quebrado bloqueia o merge — SPEC-0006:IT-02
- [ ] As regras de arquitetura do ADR-0001 falham no CI diante de uma violação — SPEC-0007:IT-06
- [ ] O trace de uma navegação aparece de ponta a ponta no Seq, sem dados pessoais — SPEC-0009:E2E-01
- [ ] Endpoint sem política de autorização não passa no CI — SPEC-0010:IT-01
- [ ] A agenda usa o design system escuro aprovado no Stitch, sem violações de acessibilidade — SPEC-0011:E2E-01

## 8. Questões em Aberto
- [x] Quem vai construir e manter e em qual linguagem? — Back-end em .NET no estilo Modular Monolith com Vertical Slices (Milan Jovanovic); front em TypeScript (recomendação aceita). Precisa ser modular para integrar IA e WhatsApp depois. (Thomas, 2026-10-13)
- [x] Orçamento mensal de hospedagem e serviços no lançamento? — Hospedagem em VPS própria (Thomas, 2026-10-13)
- [x] Qual VPS? — VPS própria já existente (Thomas, 2026-10-13)
- [x] Quando decidir o que gera custo? — Só no final, quando tudo estiver redondo localmente: VPS, domínio, e-mail, backup e monitor vão para o épico de go-live SPEC-0003; os detalhes da VPS (provedor, região, recursos) são levantados lá (Thomas, 2026-10-13)
- [x] Onde fica o repositório e onde o trabalho é acompanhado? — GitHub, acompanhamento só no repositório (tracker: none) (Thomas, 2026-10-13)
- [x] Como produtores, bandas e fãs entram? — Link mágico por e-mail + login com Google (Thomas, 2026-10-13)
- [x] O domínio já está registrado? — Não; registrar um .br (Thomas, 2026-10-13). Consulta ao Registro.br em 2026-10-13: subsolo.com.br ocupado (expira em 2027-01-16); livres: subsolo.app.br, subsolo.art.br, agendasubsolo.com.br
- [x] Qual domínio registrar? — agendasubsolo.com.br (Thomas, 2026-10-13); o registro (pago) é feito pelo Thomas no go-live (SPEC-0003)
- [x] Prazo desejado para o primeiro lançamento? — Sem prazo fixo; entrega por ondas integradas na `main`; produção a partir do go-live (Thomas, 2026-10-13)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-17 | G5 | trabalho | Integração da onda 5: (1) endpoints de teste da SPEC-0009 (PersonalDataEndpoint, PersonalDataExceptionEndpoint) recebem 401 da política de fallback da SPEC-0010; (2) o next/image do Card (SPEC-0011) injeta style="color:transparent" e a CSP estrita da SPEC-0010 bloqueia (style-src-attr) — SPEC-0010:E2E-01 falha no mobile-safari | Bateria G5 local em b67ec6f: test/integration 2 falhas, e2e 2 falhas | Architect | Ação de integração em sdd/SPEC-0001: 067d527/d82c639 (dublês de teste da SPEC-0009 anônimos + token anti-forgery), 9f2c3cc (Card sem next/image), f27dcf4 e b5bebf7 (prontidão do web atrás do Caddy; falso positivo do WebKit no <select>, Emenda 4 da SPEC-0010); bateria G5 local verde em b5bebf7 | 2026-10-17 |

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0001` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
