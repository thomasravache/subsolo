---
id: SPEC-0002
title: Agenda Subsolo — lançamento (MVP)
tier: epic
type: feature
status: approved
created: 2026-10-13
depends_on: [SPEC-0001]
adrs: []
external: []
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0002 — Agenda Subsolo — lançamento (MVP) (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0002`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
Uma agenda de shows de metal underground por cidade, alimentada por produtores e bandas aprovados, em que o fã descobre o que vai rolar, segue cidades, bandas e casas e é avisado antes do show. Resolve a divulgação que hoje se perde em stories de 24 h, grupos de WhatsApp e posts escondidos pelo algoritmo.

**Começa por uma cidade** até cobrir praticamente todos os shows dela. Metas dos primeiros 6 meses: 80% dos shows underground da cidade inicial cadastrados; 1.000 fãs seguindo a cidade; 30 produtores/bandas cadastrando sem serem chamados; relatos de público a mais.

**Público:** fãs de metal extremo (18–50 anos, quase todos no celular); produtores; bandas; casas de show e bares.

**Regras inegociáveis:** gratuito para fãs, bandas e produtores; sem anúncios na agenda; cada página leva para as redes da banda/casa (complementa o Instagram, não compete); arte extrema de flyer é bem-vinda; bandas e eventos ligados a discurso de ódio (incluindo NSBM) não entram; visual escuro, com a cara da cena.

## 2. Escopo
**Objetivos (dentro do escopo — lançamento):**
- Cadastro rápido de show (< 2 min): data, horário, local, line-up, flyer, preço, link do ingresso (vendido fora).
- Agenda por cidade com filtro por data e estilo (death, black, grind, doom, thrash).
- Página do show com prévia bonita ao compartilhar (flyer na prévia do link — Open Graph) e botões de compartilhar no WhatsApp/Instagram.
- Seguir cidades, bandas e casas; aviso de show novo e lembrete na véspera.
- Botão "vou" com contagem.
- Perfil simples de banda, casa e produtor com shows futuros e passados e links para as redes.
- Denúncia de show falso ou cancelado; aprovação manual de cada novo produtor e banda pelo administrador.

**Logo depois (épicos futuros, fora deste):** carona por show; cadastro a partir do flyer (extração automática); adicionar à agenda do celular; merch em pré-venda.
**Se der tempo (fora deste):** mapa dos shows da semana; aviso de cancelamento/alteração para quem marcou "vou"; agenda embutível em site/link da bio.

**Não-objetivos:** venda de ingressos; música, streaming ou discografia (linkar Encyclopaedia Metallum); feed, comentários ou rede social; app nas lojas.

## 3. Arquitetura Alvo
**Contexto:** projeto novo; stack e arquitetura definidas nos ADRs da fundação (SPEC-0001). Este épico só começa depois que a fundação estiver `implemented`.

Cada feature é um vertical slice num dos módulos do ADR-0001. Os módulos conversam por eventos de integração:

```
Profiles ──ProfileApproved/ProfileClaimed──> Users (papel artist)
Events ──ShowPublished / ShowStatusChanged──> Notifications (aviso de show novo; tira dos lembretes)
Engagement ──AttendanceMarked / FollowAdded──> Notifications (lembrete de véspera; destinatários)
Moderation ──ReportResolved──> Events (marca cancelado/remove)
Events <──consulta pública IProfilesApi── Profiles (nomes e links de bandas e casas na line-up)
```

**Decisões (ADRs):** ADR-0001, ADR-0002, ADR-0003, ADR-0013, ADR-0014 e ADR-0015 (já propostos na fundação); decisões novas que surgirem nas filhas viram ADRs próprios.

**Regras de arquitetura a garantir (G3):** as do ADR-0001 para cada módulo novo (aplicadas automaticamente pela SPEC-0007), + só o `Notifications` referencia push/SMTP (ADR-0014), + só o `Common.Infrastructure` referencia NetVips (ADR-0015).

## 4. Decomposição
Planejamento inicial. As specs filhas são criadas e detalhadas quando a fundação fechar, e cada spec com tela tem as suas telas aprovadas no Stitch antes do próprio H1 (decisão do Thomas, 2026-10-13).

**Revisão de 2026-10-17 (Architect):** "Contas e login" não cabia numa spec M. Ficou dividida em módulo `Users` (SPEC-0014), link mágico (SPEC-0016), Google e Minha conta. O e-mail do link mágico exige o módulo `Notifications` antes (ADR-0014: só ele fala com SMTP), por isso entrou a SPEC-0018, e "Inscrição de push" passa a estender esse módulo. A SPEC-0015 resolve a questão em aberto do título na navegação no cliente. As specs filhas são detalhadas por onda, com as telas no Stitch.

**Telas no Stitch (projeto 15194126668426659178), por spec — atualizado em 2026-10-17:**
| Spec | Tela (id) |
|---|---|
| Agenda com filtros | "Agenda Curitiba - Subsolo" (5d1946463ad9484b83c67cc2e6d78fd3, mobile) e "Agenda Curitiba - Desktop" (a9c7a58a62a5431bb833a132609c0c70) — conferir os filtros no H1 |
| Página do show | "Página do Show - Noite da Peste II" (98cc07285ec5439197d3273da0f7a11d, mobile) e desktop (ddae50bdb35f4cdaa2437a12f23fe257) |
| Cadastro de show + flyer | "Cadastrar Show - Subsolo" (0eda9cdf97fa484188933d53f1605783) |
| Perfis (pedido, aprovação, reivindicar) — SPEC-0021 | "Meus perfis" (90a9b7cb839a4992a9500362c48ec9e1), "Pedir Perfil" (af62730f71414b6082259d068b025b00), "Reivindicar Perfil" (75b72cdf03284a5998d07d0fd8ae6543) e "Fila do Admin" (600bfe18de6e44a995790033ab0f817e) |
| Perfil público | "Perfil da Banda - Podridão - Subsolo" (bdf4a66e751846d5a73dca2f606aac6a) |
| Seguir + push e preferências | "Avisos e Seguindo - Subsolo" (35b282364c47425c9e9cfe89e7b1c1e2) |
| Denúncia e moderação | "Denunciar Show - Subsolo" (9d6f88a63299478789478394c15bdd56) e a "Fila do Admin" |
| Apoie, privacidade e termos | "Apoie o Subsolo" (gerada; id a confirmar no canvas — a ferramenta estourou o tempo) |
| Minha conta | SPEC-0019 |
| Entrar com Google | botão já na tela "Entrar - Subsolo" (SPEC-0016) |

**Radar LGPD (pedido do Thomas, 2026-10-17):** o que já está coberto e o que entra em cada spec.
- Já coberto: classificação dos dados, base legal e retenção (ADR-0008); TLS e HSTS (SPEC-0010); backup criptografado (ADR-0011); token e sessão guardados só como hash e e-mail mascarado nos logs (SPEC-0014 e SPEC-0016); resposta igual exista a conta ou não.
- "Minha conta" (onda 4+): exportar e excluir os próprios dados, com teste de que a exclusão apaga a conta, as sessões, os tokens, os "vou" e os seguidos.
- "Apoie, privacidade e termos": política de privacidade com finalidades, bases legais e retenção do ADR-0008; canal de contato do titular (agente de pequeno porte dispensado de encarregado, Resolução CD/ANPD nº 2/2022, mas o canal é obrigatório); aviso de que dados excluídos somem dos backups em até 6 meses (retenção do ADR-0011); plano curto de resposta a incidente (comunicação à ANPD e aos titulares, Resolução CD/ANPD nº 15/2024) em `docs/runbooks/`.
- Go-live (SPEC-0003): criptografia em repouso do disco do banco e dos flyers (questão aberta lá).
- Criptografia por coluna: adiada por decisão do Thomas em 2026-10-17 (ADR-0008, atualização da mesma data); volta se entrar dado mais delicado.

**Herança da SPEC-0015 (Emenda 1, 2026-10-17):** a primeira spec que criar uma página interna que responda 200 e seja alcançável por `next/link` (Página do show ou Apoie) leva o E2E do título na navegação no cliente: um `MutationObserver` registra todo `document.title` durante o clique, nenhum valor fica vazio e o axe `document-title` passa logo depois da troca de URL. Achado junto: a página de cidade inexistente carregada direto mostrou o título "Subsolo" em vez de "Cidade não encontrada · Subsolo" — investigar na mesma spec.

| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0014 | Módulo Users: conta, sessão e papéis | full | feature | M | fundação | — |
| SPEC-0015 | Título da página presente na navegação no cliente | lite | fix | S | fundação | — |
| SPEC-0018 | Módulo Notifications: e-mail transacional por SMTP | full | feature | M | fundação | — |
| SPEC-0016 | Entrar por link mágico no e-mail | full | feature | M | SPEC-0014 | SPEC-0018 |
| SPEC-0017 | Cidades e estilos no painel do admin | full | feature | M | SPEC-0014, SPEC-0016 | — |
| SPEC-0020 | Entrar com Google (provedor OIDC falso no local e no CI; cliente real no go-live) | full | feature | M | SPEC-0016 | — |
| SPEC-0019 | Minha conta: nome, exportar dados e excluir conta | full | feature | M | SPEC-0016 | — |
| SPEC-0021 | Perfis: pedido de cadastro, aprovação pelo admin, perfil sem dono e reivindicação | full | feature | M | Contas | — |
| SPEC-0022 | Upload e processamento de flyer (inclui o `IFileStorage` em disco, saído da SPEC-0007 na Emenda v1) | full | feature | S | fundação | — |
| a criar | Cadastro de show (< 2 min), edição, cancelado/adiado | full | feature | M | Perfis, Cidades | Flyer |
| a criar | Agenda da cidade com filtro por data e estilo | full | feature | M | Cadastro de show | — |
| a criar | Página do show com Open Graph e compartilhar | full | feature | M | Cadastro de show | Flyer |
| a criar | Perfil público de banda, casa e produtor (futuros e passados) | full | feature | M | Perfis | Cadastro de show |
| a criar | Seguir cidade, banda e casa | full | feature | S | Contas | Perfis |
| a criar | "Vou" com contagem | full | feature | S | Contas | Cadastro de show |
| a criar | Inscrição de push, preferências e canal de e-mail | full | feature | M | Contas | — |
| a criar | Aviso de show novo (push imediato + resumo diário por e-mail) | full | feature | M | Inscrição de push | Seguir, Cadastro de show |
| a criar | Lembrete na véspera | full | feature | S | Inscrição de push | "Vou" |
| a criar | Denúncia de show e fila de moderação | full | feature | M | Contas | Cadastro de show |
| a criar | Páginas Apoie (Pix), privacidade e termos | full | feature | S | fundação | — |

## 5. Estratégia de Entrega
- **Ambientes:** local e CI até o go-live (SPEC-0003); depois, staging automático a cada merge e produção com confirmação por release.
- **Entrega por onda:** cada onda fecha um pedaço da jornada (contas → cadastro → agenda e página do show → seguir/vou → avisos → moderação); antes do go-live, tudo fica na `main`, validado pelo Compose local.
- **Feature flags:** `avisos-email` — liga o resumo diário por e-mail só depois de o provedor ser configurado no go-live; removida após 2 semanas estável.
- **Rollback:** reverter o merge da onda; em produção, deploy da tag anterior (ADR-0004).
- **Métricas de sucesso pós-release:** as metas de 6 meses do PRD (seção Visão), acompanhadas no painel do admin: % de shows da cidade cadastrados, seguidores da cidade, produtores cadastrando sem convite.

## 6. Riscos & Mitigações
- Produtores não cadastram sozinhos no começo — mitigação: o admin cadastra em perfis sem dono, que os produtores reivindicam depois (resposta do Thomas).
- Push no iPhone exige o site na tela de início — mitigação: e-mail como canal de garantia e um convite para instalar mostrado depois do primeiro "vou".
- Bandas ou eventos ligados a discurso de ódio (NSBM) — mitigação: aprovação manual de todo novo produtor e banda, denúncia em toda página e remoção pelo admin.
- Show falso ou cancelado em cima da hora — mitigação: selo de cancelado/adiado, saída dos lembretes e denúncia.
- Prévia do WhatsApp não mostrar o flyer — mitigação: imagem `og` 1200×630 dedicada (ADR-0015) e E2E que confere as meta tags.

## 7. Critérios de Aceite do Épico
Os IDs de teste são definidos quando as filhas forem criadas; cada critério abaixo vai citar o E2E que o prova.
- [ ] Produtor aprovado cadastra um show com flyer, line-up, casa, preço e link de ingresso em menos de 2 minutos [jornada: cadastro-show]
- [ ] O fã vê a agenda da cidade e filtra por data e estilo [jornada: agenda]
- [ ] O link do show compartilhado no WhatsApp mostra o flyer na prévia [jornada: compartilhar-show]
- [ ] O fã segue cidade, banda ou casa, recebe o aviso de show novo e o lembrete na véspera do show em que marcou "vou" [jornada: seguir-e-avisar]
- [ ] Perfil de banda, casa e produtor lista os shows futuros e passados, com links para as redes
- [ ] Denúncia de show falso ou cancelado chega à fila do admin, que resolve
- [ ] Nenhum anúncio na agenda; a página "Apoie" fica acessível pelo rodapé

## 8. Questões em Aberto
- [x] Nome do produto? — "Subsolo", nome da família de produtos (Thomas, 2026-10-13)
- [x] Qual é a cidade inicial? — O sistema nasce multi-cidade; a cidade inicial (a do Thomas, conforme o PRD) é um dado cadastrado pelo admin, não código, e não bloqueia a construção (recomendação aceita pelo Thomas, 2026-10-13)
- [x] Canal de aviso no lançamento? — Push no navegador (PWA) + e-mail; WhatsApp entra depois como mais um canal do módulo de avisos (Thomas, 2026-10-13)
- [x] O fã precisa criar conta para seguir e marcar "vou"? — Agenda e páginas abertas sem conta; seguir e "vou" exigem login (Google ou link por e-mail) (Thomas, 2026-10-13)
- [x] O administrador cadastra shows em nome de produtores no começo? — Sim: o admin cadastra em perfis ainda sem dono; quando o produtor/banda entra e é aprovado, assume o perfil com o histórico (Thomas, 2026-10-13)
- [x] Show cancelado em cima da hora: o que acontece no lançamento? — Produtor (ou admin, após denúncia) marca "cancelado" ou "adiado"; selo visível na página e na agenda, e o show sai dos lembretes. Aviso ativo a quem marcou "vou" segue em "se der tempo" (Thomas, 2026-10-13)
- [x] Lista de estilos? — Começa com death, black, grind, doom e thrash; o admin adiciona outros pelo painel; um show pode ter vários estilos (Thomas, 2026-10-13)
- [x] Apoio via Pix e marca apoiadora no lançamento? — Página "Apoie" estática com chave/QR Pix e lista de apoiadores, com link discreto no rodapé; nada no meio da agenda; sem integração de pagamento (Thomas, 2026-10-13)
- [x] Como será feito o design das telas? — Stitch (mobile e desktop): design system escuro primeiro, depois as telas de cada spec de interface antes do H1 dela (Thomas, 2026-10-13)
- [x] Navegação no cliente fica ~300 ms sem `<title>` no Next 16 (metadados em bloco separado; achado na SPEC-0011, Emenda 3) — Metadados bloqueantes com `htmlLimitedBots` global, provados por teste axe `document-title` depois de navegar; se o teste mostrar que não resolve a navegação no cliente, os links afetados passam a navegação completa (Thomas, 2026-10-17)
- [x] Como fazer as telas no Stitch? — Por onda: as telas das specs de cada onda são feitas e aprovadas antes do H1 delas (Thomas, 2026-10-17)
- [x] Quando criar o cliente OAuth do Google? — No go-live (SPEC-0003); localmente e no CI, provedor OIDC falso em contêiner e link mágico pelo Mailpit (Thomas, 2026-10-17)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0002` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
