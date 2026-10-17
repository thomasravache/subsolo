---
id: SPEC-0018
title: "Módulo Notifications: e-mail transacional por SMTP"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-17
parent: SPEC-0002
depends_on: []
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Notifications/**, tests/Modules/Notifications/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/Subsolo.Api.csproj, src/Subsolo.Api/packages.lock.json, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, src/Subsolo.AppHost/**, tests/Subsolo.ArchitectureTests/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, Directory.Packages.props, Subsolo.slnx, tests/**/packages.lock.json]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0010, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-17
---

# SPEC-0018 — Módulo Notifications: e-mail transacional por SMTP

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Cria o módulo `Notifications` com o primeiro canal do ADR-0014: **e-mail transacional por SMTP**, com o Mailpit no ambiente local e no CI e o provedor real escolhido no go-live. Outros módulos mandam e-mail só pela API pública do `Notifications`. O primeiro uso é o link mágico de login (SPEC-0016); os avisos de show novo e o lembrete usam o mesmo canal depois.

## 2. Motivação & Escopo
**Motivação:** o login por link mágico precisa mandar e-mail, e o ADR-0014 diz que só o `Notifications` fala com SMTP. Sem este módulo, o `Users` teria de mandar e-mail por conta própria e quebraria a regra.

**Objetivos (dentro do escopo):**
- Módulo `Notifications` no padrão do ADR-0001, ainda sem tabelas; o schema `notifications` nasce com as preferências e a inscrição de push.
- API pública `INotificationsApi.SendEmailAsync` em `Subsolo.Modules.Notifications.IntegrationEvents`, com modelos de mensagem fechados (enum). Nesta spec só existe o modelo `MagicLink`.
- Modelo `MagicLink` em pt-BR, em HTML e em texto puro: assunto "Seu link para entrar no Subsolo", botão "Entrar no Subsolo", validade em minutos e o aviso "Se não foi você, ignore este e-mail."
- Envio por SMTP com MailKit (recomendação da Microsoft no lugar do `System.Net.Mail.SmtpClient`). A configuração fica em `Notifications:Smtp` (host, porta, TLS, usuário, senha, remetente). No local e no CI, a referência `smtp` do Mailpit vem do AppHost; a senha de produção fica fora do repositório (ADR-0008).
- Timeout de 10 s por envio; SMTP fora do ar devolve `notifications.email_unavailable`, sem derrubar a requisição.
- Regra de arquitetura (G3): só o `Notifications` referencia MailKit.

**Não-objetivos (fora do escopo):**
- Push, preferências, resumo diário, aviso de show novo e lembrete (specs próprias do épico).
- Fila ou outbox para o e-mail transacional: o link mágico é síncrono (o usuário espera o envio e pode pedir de novo); guardar o link na outbox gravaria o token em claro no banco.
- Webhooks de bounce e provedor de produção (go-live, SPEC-0003).

## 3. Dependências
- **Implementações necessárias:** N/A — só a fundação (SPEC-0001, `implemented`).
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** Mailpit no AppHost (já existe, SPEC-0007); pacote MailKit, com a versão estável conferida no NuGet no G1 e fixada no `Directory.Packages.props` (ADR-0009).

## 4. Decisão Arquitetural
**Contexto:** padrão do módulo `Events` (SPEC-0008): projetos `Domain`, `Application`, `Infrastructure`, `Presentation` e `IntegrationEvents`, com registro por `Add<M>Module`. A API pública entre módulos fica em `<Módulo>.IntegrationEvents` (ADR-0001, alinhado na SPEC-0013).

**Decisão:** módulo `Notifications` com `INotificationsApi` público e a implementação `SmtpEmailChannel : INotificationChannel` interna à Infrastructure, como manda o ADR-0014 (canais plugáveis). Os modelos de mensagem são classes internas com HTML escapado. O chamador nunca passa HTML: passa só o modelo e os dados tipados.

**Justificativa:** segue o ADR-0014 (só o `Notifications` fala com SMTP, canal plugável) e o ADR-0008 (o chamador não monta conteúdo, então não há injeção de HTML; o e-mail do destinatário fica mascarado nos logs).

**Desvio do padrão existente:** Nenhum. O MailKit é dependência nova, coberta pelo ADR-0014 (e-mail por SMTP padrão) ; versão fixada no `Directory.Packages.props` e auditada pelo job `security` (ADR-0009).

**Alternativas descartadas:** `System.Net.Mail.SmtpClient` (a Microsoft não recomenda para código novo, e falta suporte a protocolos modernos); SDK de provedor (amarra o provedor antes do go-live, contra o ADR-0014); evento de integração com o link (grava o token em claro na outbox).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0010, ADR-0014

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** envio ao Mailpit em ≤ 2 s p95 (IT-01, 20 envios); timeout de 10 s por envio (IT-03).
- **Segurança:** os dados do modelo são escapados no HTML, então um nome com `<script>` chega como texto (UT-02); TLS obrigatório fora de Development (`StartTls` ou `SslOnConnect`), recusando a inicialização com TLS desligado em Production (UT-03); credenciais só por configuração ou segredo.
- **Privacidade e dados pessoais:** o e-mail do destinatário aparece mascarado (`f***@email.com`) em logs e spans, e o conteúdo da mensagem nunca é logado, porque o link é uma credencial (IT-04).
- **Disponibilidade e resiliência:** SMTP fora do ar ou lento → `Result` com `notifications.email_unavailable` em até 10 s, sem exceção para o chamador (IT-03); sem retry automático no envio síncrono, porque o usuário pede de novo.
- **Acessibilidade (UI):** o e-mail HTML tem `lang="pt-BR"`, versão em texto puro, botão com texto (não imagem) e contraste AA nas cores do design system (UT-01).
- **Custo:** zero agora (Mailpit); no go-live, o provedor escolhido na SPEC-0003 (recomendação no ADR-0014: Amazon SES, US$ 0,10 por 1.000).

## 6. Artefato A — Contrato
**Interface:** `INotificationsApi.SendEmailAsync` (público, em `Subsolo.Modules.Notifications.IntegrationEvents`)

```csharp
public interface INotificationsApi
{
    Task<Result> SendEmailAsync(EmailRequest request, CancellationToken cancellationToken);
}

public sealed record EmailRequest(string To, EmailTemplate Template, IReadOnlyDictionary<string, string> Data);

public enum EmailTemplate { MagicLink }
// MagicLink — Data obrigatório: "link" (URL absoluta https, ou http só em Development), "minutes" (inteiro 1–60)

// Erros (Result.Failure):
//   notifications.invalid_recipient  — To fora do formato de e-mail ou > 254 caracteres
//   notifications.invalid_data       — chave obrigatória ausente ou valor inválido para o modelo
//   notifications.email_unavailable  — SMTP fora do ar, recusou ou passou de 10 s
```

```text
Configuração (Notifications:Smtp): Host, Port, Security (None|StartTls|SslOnConnect), User, Password, FromAddress, FromName="Subsolo"
  Development/CI: referência "smtp" do Mailpit (AppHost), Security=None
  Fora de Development: Security ≠ None obrigatório (a API não sobe sem isso); senha por segredo (ADR-0008)
  Compose publicado: Host/Port do endpoint smtp do Mailpit e Security = ${SMTP_SECURITY:-StartTls} (a pilha local do compose-up.sh usa None em Development) — Emenda 2
E-mail MagicLink: assunto "Seu link para entrar no Subsolo"; HTML + texto; botão "Entrar no Subsolo" → {link};
  "O link vale por {minutes} minutos e só funciona uma vez."; "Se não foi você, ignore este e-mail."
```

**Design:** e-mail simples no tema do design system (fundo #0B0B0C, texto #E8E2D4, botão #D7263D com texto branco, wordmark SUBSOLO em texto). Não há tela no Stitch, porque é um e-mail transacional.

**Arquivos/módulos afetados:** ver `touches` no frontmatter. Novo `src/Modules/Notifications/` (5 projetos) e `tests/Modules/Notifications/`; registro no `Program.cs`, no `Subsolo.slnx` e no AppHost (referência do SMTP já existe); nova regra no `ModularMonolithRules`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Envio do link | destinatário válido, `link` e `minutes` válidos | e-mail chega ao Mailpit com assunto, HTML, texto e o link exato | UT-01, IT-01 |
| Destinatário inválido | `To` sem arroba ou > 254 caracteres | `notifications.invalid_recipient`, nada enviado | UT-02 |
| Dados inválidos | `link` ausente, não absoluto ou `minutes` fora de 1–60 | `notifications.invalid_data`, nada enviado | UT-02 |
| Escape de HTML | dado com `<script>` | aparece como texto no HTML | UT-02 |
| SMTP fora do ar | servidor parado ou sem resposta | `notifications.email_unavailable` em ≤ 10 s, sem exceção | IT-03 |
| TLS fora de Development | `Security=None` em Production ou Staging | a API não inicia (validação de opções) | UT-03 |
| Dados pessoais fora dos logs | envio com logs e spans capturados | e-mail mascarado, conteúdo e link ausentes | IT-04 |
| Só o Notifications usa SMTP | arquitetura | nenhum outro projeto referencia MailKit | IT-05 |
| AppHost liga o SMTP | `aspire run` e o Compose publicado | a API recebe Host/Port do Mailpit; no publicado, Security vem de `SMTP_SECURITY` com padrão `StartTls` | IT-02 |
| API pública estável | consumidor (SPEC-0016) | assinatura, enum e códigos de erro do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — módulo novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o modelo `MagicLink` com `link=https://subsolo.test/entrar/confirmar?token=abc` e `minutes=15`, quando renderizado, então o assunto, o HTML (`lang="pt-BR"`, botão com o link, "15 minutos") e o texto puro contêm o link exato e as frases do §6, e as cores do botão têm contraste AA.
- **UT-02** — Dados `To="sem-arroba"`, `To` com 255 caracteres, `link` ausente, `link="/relativo"`, `minutes=0` e `minutes=61`, quando `SendEmailAsync` valida, então retorna `notifications.invalid_recipient` ou `notifications.invalid_data` sem chamar o canal; dado um dado com `<script>`, então ele aparece escapado no HTML.
- **UT-03** — Dado o ambiente Production ou Staging com `Notifications:Smtp:Security=None`, quando as opções são validadas na inicialização, então falha com mensagem clara; em Development, aceita.

### 7.3 Testes de Integração
- **IT-01** — Dado o Mailpit real em container (Testcontainers), quando `SendEmailAsync` manda o `MagicLink` 20 vezes, então a API HTTP do Mailpit mostra as 20 mensagens com remetente "Subsolo", assunto, partes HTML e texto e o link exato, e o p95 do envio fica ≤ 2 s.
- **IT-02** — Dado o modelo do AppHost (teste do AppHost, padrão da SPEC-0007), quando lido, então a API tem a referência `smtp` do Mailpit e a seção `Notifications:Smtp` resolvida para ela; no modo publish (Compose), também Host e Port do Mailpit e `Security=${SMTP_SECURITY:-StartTls}` (Emenda 2).
- **IT-03** — Dado um servidor TCP que aceita a conexão e não responde, e depois uma porta fechada, quando `SendEmailAsync` é chamado, então retorna `notifications.email_unavailable` em ≤ 10 s nos dois casos, sem lançar exceção.
- **IT-04** — Dados os logs e spans capturados (coletor em memória do OpenTelemetry) durante um envio, quando inspecionados, então o destinatário aparece só mascarado, e nem o link nem o corpo aparecem.
- **IT-05** — Dados os testes de arquitetura, quando rodam, então só os projetos `Subsolo.Modules.Notifications.*` referenciam `MailKit`/`MimeKit`, e um projeto de violação de propósito (padrão `Subsolo.Common.ArchitectureViolations`) faz a regra falhar.

### 7.4 Testes de Contrato
- **CT-01** — Dado o assembly `Subsolo.Modules.Notifications.IntegrationEvents`, quando inspecionado, então `INotificationsApi`, `EmailRequest` e `EmailTemplate.MagicLink` têm as assinaturas do §6, e os códigos de erro são constantes públicas com os valores do §6. A SPEC-0016 consome esse contrato.

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário muda nesta spec. O e-mail aparece na jornada de entrar da SPEC-0016, cujo E2E lê a mensagem no Mailpit.

### 7.6 Outros
- Segurança: checklist OWASP no G4 (injeção de cabeçalho no assunto ou no destinatário com `\r\n` rejeitada pelo validador de e-mail; escape do HTML).

**Dublês e dados de teste:** Mailpit em container; servidor TCP silencioso para o timeout; destinatários `@subsolo.test`.

**Ambiente de execução:** IT com Testcontainers local e no CI, em série (`--max-parallel-test-modules 1`).

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003).
- **Dados/schema:** N/A — sem tabelas nesta spec.
- **Compatibilidade:** API pública nova; mudanças incompatíveis exigem emenda e `contract_version` novo.
- **Observabilidade:** span `notifications.email.send` com o modelo e o resultado; contador `subsolo.notifications.emails` (modelo, resultado); log de falha com o destinatário mascarado.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Contrato e validação**
- [ ] Red: UT-01..UT-03 com a tag `SPEC-0018:<ID>`, falhando pelo motivo certo
- [ ] Green: módulo `Notifications` (padrão do `Events`), `INotificationsApi` em `IntegrationEvents`, modelo `MagicLink`, validação de destinatário e dados
- [ ] Refactor mantendo tudo verde

**Fase 2: SMTP real**
- [ ] Red: IT-01..IT-05 e CT-01 falhando pelo motivo certo
- [ ] Green: envio com MailKit (dependência nova; ADR-0009), configuração `Notifications:Smtp` com TLS obrigatório fora de Development, timeout de 10 s, Mailpit no AppHost
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 93aac2d (árvore suja) | 2026-10-17 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 3cac14d | 2026-10-17 |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | FAIL | Reviewer: CHANGES_REQUESTED em 59d9d11 — major: SMTP não ligado ao Mailpit no Compose publicado (E2E da SPEC-0016 depende); menores: QUIT falho vira email_unavailable, regex de destinatário mais frouxa que o MimeKit, regra SMTP sem os hosts, TLS só em Production vs §5, comentário do csproj desatualizado | 2026-10-17 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-17 | G1 | spec | Contrato §6 (Task<Result> em Notifications.IntegrationEvents) conflita com a regra do ADR-0001 'IntegrationEvents só depende de Common.Application' — Result/Error vivem em Common.Domain; guardas SPEC-0007 IT-06 e SPEC-0014 IT-09 reprovam | Test-writer: scaffold exato do §6; único conflito é o Result | Architect | Emenda 1 (arquitetura) — ADR-0001 esclarecido: Result/Result<T>/Error/ErrorType de Common.Domain permitidos em IntegrationEvents; regra de arquitetura atualizada | 2026-10-17 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0018`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (comportamento e testes) | 2026-10-17 | Emenda 2: TLS obrigatório fora de Development (alinha §6 e UT-03 ao §5); Compose publicado ligado ao Mailpit com `SMTP_SECURITY` (padrão `StartTls`); testes novos: UT-02 com endereços que o MimeKit recusa (`a>@x.com`, `a@b.com>,<c`) → `invalid_recipient`, UT-03 com Staging, IT-02 no modo publish, IT-05 avaliando também `Subsolo.Api` e `Subsolo.ServiceDefaults` | Revisão G4 (CHANGES_REQUESTED): o E2E da SPEC-0016 roda no Compose e não recebia SMTP; regex de destinatário mais frouxa que o MimeKit; regra do ADR-0014 sem os hosts | SPEC-0016 (usa `SMTP_SECURITY=None` no `compose-up.sh`, já nos `touches` dela) | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 1 (arquitetura) | 2026-10-17 | Sem mudança no contrato: o ADR-0001 foi esclarecido para permitir em `IntegrationEvents` os tipos `Result`, `Result<T>`, `Error` e `ErrorType` de `Common.Domain` (e só eles); a regra `IntegrationEventsDependencies` dos testes de arquitetura é atualizada pelo Test-writer com essa exceção e um exemplo que reprova entidade de domínio | IMP-01: o `Task<Result>` do §6 reprovava as guardas do ADR-0001 | nenhuma (SPEC-0016 segue fixada em @1) | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
| 1 (escopo) | 2026-10-17 | `touches` ganha `tests/**/packages.lock.json` | Referenciar um módulo novo no `Subsolo.Api` regrava o lock dos projetos de teste que referenciam a API (o CI restaura em modo travado); achado no verify da SPEC-0014 | nenhuma | thomas (delegado: piloto automático, 2026-10-17, recomendação do Architect) |
