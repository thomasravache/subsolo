---
id: SPEC-0030
title: Inscrição de push, preferências de aviso e canal de e-mail
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0019, SPEC-0027]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Notifications/**, tests/Modules/Notifications/**, src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, src/Subsolo.Api/packages.lock.json, src/Subsolo.AppHost/**, Directory.Packages.props, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/seguindo/**, web/src/app/(public)/avisos/**, web/src/app/manifest.ts, web/public/sw.js, web/public/icons/**, web/src/components/notifications/**, web/src/proxy.ts, web/src/proxy.test.ts, tools/compose-up.sh, e2e/tests/avisos/**, docs/design/**, src/Common/Subsolo.Common.Presentation/**, tests/Common/Subsolo.Common.UnitTests/**]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0013, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-18
---

# SPEC-0030 — Inscrição de push, preferências de aviso e canal de e-mail

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O fã ativa os avisos no celular (Web Push, com o Subsolo instalado como app no iPhone) e escolhe o que quer receber: show novo de quem segue (na hora, por push), resumo diário por e-mail e lembrete na véspera dos shows em que marcou "vou". O módulo `Notifications` ganha o canal Web Push (VAPID, sem intermediário), a inscrição dos navegadores, as preferências, o descadastro por link em todo e-mail e a entrada única `EnqueueAsync` que as specs de aviso vão usar. O site vira PWA instalável.

## 2. Motivação & Escopo
**Motivação:** ADR-0013 (PWA com push) e ADR-0014 (push + e-mail, canais plugáveis, preferências, descadastro, inscrições inválidas removidas). Base das specs "Aviso de show novo" e "Lembrete na véspera".

**Objetivos (dentro do escopo):**
- PWA: `manifest.webmanifest` (nome, ícones, cores do design system, `display: standalone`) e service worker próprio (`/sw.js`) só para push e clique na notificação (abre a URL do aviso); sem cache offline além do mínimo.
- `POST /api/v1/me/push-subscriptions` (endpoint, chaves p256dh/auth) e `DELETE` (pelo endpoint); `GET /api/v1/push/public-key` (chave pública VAPID).
- Preferências por usuário: `newShowPush` (padrão ligado ao ativar o push), `dailyDigestEmail` (padrão desligado; opt-in, ADR-0008), `reminderPush` (padrão ligado) e `reminderEmail` (padrão desligado): `GET` e `PUT /api/v1/me/notification-preferences`.
- Canal `WebPush` (`INotificationChannel`, ADR-0014) com payload criptografado e assinatura VAPID; respostas 404/410 do serviço de push removem a inscrição.
- `INotificationsApi.EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice)` — enfileira um aviso (`kind` new_show | reminder | digest, `dedupeKey`, título, corpo, url) respeitando as preferências e os canais disponíveis; idempotente pela chave `(usuário, dedupeKey)` (ADR-0014); entrega pela outbox com novas tentativas.
- `IRecipientDirectory.GetEmailsAsync(userIds)` definido no contrato do `Notifications` e implementado pelo `Users` (o `Notifications` não depende de módulo de domínio, ADR-0001/0014).
- Descadastro: todo e-mail de aviso leva um link `/avisos/descadastrar?token=` (token assinado, sem login) que desliga aquele tipo de e-mail.
- Conta: `INotificationsApi.ForgetUserAsync(userId)` (o `Users` chama na exclusão) e `ExportAsync(userId)` (o `Users` inclui no export como seção "notifications").
- Tela "Avisos" (parte de cima de "Avisos e Seguindo", em `/seguindo` ou `/conta/avisos`): "Ativar avisos" (só depois do toque, ADR-0013), situação ("Avisos ativos neste aparelho" / bloqueados / não suportado), os quatro interruptores; no iPhone sem instalar, instrução "Adicione o Subsolo à tela de início para receber avisos".

**Não-objetivos (fora do escopo):**
- Os avisos em si (show novo, resumo, lembrete): specs seguintes, que chamam `EnqueueAsync`.
- WhatsApp (canal futuro, ADR-0014).
- Cache offline da agenda.

## 3. Dependências
- **Implementações necessárias:** SPEC-0019 (exclusão e export da conta) e SPEC-0027 (tela "Seguindo", onde a parte "Avisos" mora).
- **Contratos consumidos:** N/A.
- **Pré-requisitos externos:** biblioteca .NET de Web Push com VAPID (avaliar `WebPush`/`Lib.Net.Http.WebPush`, licença MIT ou Apache-2.0, versão fixada e verificada, ADR-0009); chaves VAPID: geradas para o desenvolvimento, de produção no go-live (segredo, ADR-0008).

## 4. Decisão Arquitetural
**Contexto:** módulo `Notifications` (SPEC-0018: `INotificationsApi`, canal de e-mail, Mailpit); ADR-0014 com o esclarecimento de 2026-10-18; ADR-0001 (direção das dependências); ADR-0013 (PWA).

**Decisão:** `Notifications` ganha `PushSubscription`, `NotificationPreferences`, `NoticeDelivery` (registro idempotente por usuário + chave) e o canal `WebPush`; `EnqueueAsync` grava as entregas na outbox do módulo e um processador envia por canal com novas tentativas. O e-mail dos destinatários vem de `IRecipientDirectory` (implementado pelo `Users`). O web ganha o manifest, o service worker e a tela.

**Justificativa:** ADR-0014 (canais plugáveis, VAPID direto, idempotência, descadastro, limpeza de inscrições inválidas), ADR-0013 (PWA, botão depois do gesto), ADR-0008 (consentimento opt-in no e-mail, export e exclusão).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** FCM/OneSignal (ADR-0014); guardar o e-mail no `Notifications` (dado pessoal duplicado); o `Notifications` consultar o `Users` (ciclo, ADR-0001).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0013, ADR-0014

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `EnqueueAsync` para 10.000 destinatários em ≤ 2 s (grava as entregas em lote) e envio de push a 1.000 inscrições em ≤ 60 s contra o servidor de push falso (IT-04).
- **Segurança:** inscrições e preferências exigem `fan` + `X-CSRF`; o endpoint de inscrição aceita só `https` de serviços de push conhecidos (FCM, Mozilla, Apple) — evita SSRF; chave privada VAPID só por segredo; token de descadastro assinado (HMAC) e de uso para um tipo só; service worker sem `eval` e compatível com a CSP.
- **Privacidade e dados pessoais:** push e e-mail de aviso são por consentimento (ADR-0008): e-mail desligado por padrão, push só depois de ativar; export e esquecimento (IT-06); payload do push sem dados pessoais além do aviso.
- **Disponibilidade e resiliência:** novas tentativas com espera crescente; 404/410 remove a inscrição; um aviso nunca sai duas vezes (IT-03).
- **Acessibilidade (UI):** interruptores com rótulo e estado (`role="switch"`, `aria-checked`), mensagens de situação em `aria-live`, axe no E2E.
- **Custo:** zero (VAPID direto; e-mail pelo provedor do go-live).

## 6. Artefato A — Contrato
**Interface:** `/api/v1/push/public-key` · `/api/v1/me/push-subscriptions` · `/api/v1/me/notification-preferences` · `/avisos/descadastrar` · `INotificationsApi.EnqueueAsync/ForgetUserAsync/ExportAsync` · `IRecipientDirectory`

```yaml
GET  /api/v1/push/public-key:                     # AllowAnonymous + default ; 200 { publicKey }
POST /api/v1/me/push-subscriptions:               # fan + X-CSRF + default
  body: { endpoint: https (serviço de push conhecido), keys: { p256dh, auth } } ; 204 (idempotente pelo endpoint)
  400: notifications.invalid_subscription
DELETE /api/v1/me/push-subscriptions:             # fan + X-CSRF ; body { endpoint } ; 204
GET /api/v1/me/notification-preferences:          # fan + default ; no-store
  200: { newShowPush, dailyDigestEmail, reminderPush, reminderEmail, pushDevices: n }
PUT /api/v1/me/notification-preferences:          # fan + X-CSRF ; body com os quatro booleanos ; 200 (mesmo corpo)
GET /avisos/descadastrar?token=…:                 # página do web: confirma "Pronto: você não recebe mais {tipo} por e-mail." ; token inválido → mensagem de link inválido
POST /api/v1/notifications/unsubscribe:           # AllowAnonymous + X-CSRF + default ; body { token } ; 204 ; 400 notifications.invalid_token
```

```csharp
// Subsolo.Modules.Notifications.IntegrationEvents (público)
Task<Result> INotificationsApi.EnqueueAsync(IReadOnlyCollection<Guid> userIds, Notice notice, CancellationToken ct);
public sealed record Notice(string Kind /* new_show | reminder | digest */, string DedupeKey, string Title, string Body, string Url);
Task INotificationsApi.ForgetUserAsync(Guid userId, CancellationToken ct);
Task<object?> INotificationsApi.ExportAsync(Guid userId, CancellationToken ct);
public interface IRecipientDirectory { Task<IReadOnlyDictionary<Guid, string>> GetEmailsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct); }  // implementado pelo Users
```

```text
Banco (schema notifications): push_subscriptions(id, user_id, endpoint UNIQUE, p256dh, auth, created_at_utc, last_success_at_utc)
  preferences(user_id PK, new_show_push, daily_digest_email, reminder_push, reminder_email)
  deliveries(user_id, dedupe_key, kind, channel, status, attempts, created_at_utc; UNIQUE(user_id, dedupe_key, channel))
Web: /manifest.webmanifest ; /sw.js (push → showNotification(título, corpo, ícone) ; notificationclick → abre url)
Tela "AVISOS": "Ativar avisos" (vermelho) ; situação ; interruptores "Show novo de quem eu sigo (na hora)", "Resumo diário por e-mail",
  "Lembrete na véspera (no celular)", "Lembrete na véspera (por e-mail)" ; iPhone sem instalar → instrução da tela de início ; bloqueado → "Os avisos estão bloqueados no navegador."
```

**Design:** Stitch, projeto 15194126668426659178: "Avisos e Seguindo - Subsolo" (35b282364c47425c9e9cfe89e7b1c1e2), parte "Avisos". Ajustes: o lembrete ganha dois interruptores (push e e-mail); a instrução de instalação no iPhone usa o mesmo bloco de alerta da tela.

**Arquivos/módulos afetados:** ver `touches` no frontmatter.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ativar push | fã aceita a permissão | inscrição gravada; preferência de show novo ligada | UT-01, IT-01, E2E-01 |
| Inscrição inválida | endpoint `http`, domínio desconhecido, chaves ausentes | 400 `invalid_subscription` | UT-01, IT-01 |
| Preferências | GET/PUT | valores salvos e devolvidos; e-mail desligado por padrão | IT-02, E2E-01 |
| Enfileirar aviso | `EnqueueAsync` para N usuários | uma entrega por usuário e canal permitido; chave repetida não duplica | UT-02, IT-03 |
| Entrega push | servidor de push falso | payload criptografado, cabeçalhos VAPID, novas tentativas | IT-04 |
| Inscrição morta | servidor responde 410 | inscrição removida | IT-04 |
| E-mail e descadastro | aviso por e-mail | e-mail no Mailpit com link; o link desliga aquele tipo | IT-05, E2E-02 |
| Conta | export; exclusão | seção "notifications"; inscrições, preferências e entregas apagadas | IT-06 |
| Bloqueado / sem suporte / iPhone | permissão negada; navegador sem push | mensagem certa; nada gravado | UT-03, E2E-02 |
| Arquitetura | dependências | `Notifications` não depende de módulo de domínio; só ele usa push e SMTP | IT-07 |
| Contrato | OpenAPI e interfaces | corpos, códigos e assinaturas do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — o canal de e-mail e o `SendEmailAsync` da SPEC-0018 seguem cobertos pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dado o validador de inscrição, quando recebe endpoints `https://fcm.googleapis.com/…`, `https://updates.push.services.mozilla.com/…`, `https://web.push.apple.com/…`, `http://…`, `https://evil.com/…` e chaves ausentes, então só os três primeiros com chaves passam.
- **UT-02** — Dadas preferências variadas e um `Notice` de show novo, quando o planejador de entregas roda, então cria entregas só para os canais permitidos por usuário e ignora chaves já entregues.
- **UT-03** — Dado o componente "Avisos" (Vitest) com `Notification.permission` em `default`, `granted`, `denied` e sem suporte, e com `navigator.standalone` falso no iOS, então mostra o botão, os interruptores ou a mensagem certa, e só pede permissão depois do toque.

### 7.3 Testes de Integração
- **IT-01** — Dados a API e o PostgreSQL, quando o fã registra uma inscrição válida duas vezes e uma inválida, então 204 (um registro) e 400; `DELETE` remove.
- **IT-02** — Dado um fã, quando lê as preferências antes e depois de ativar o push e as altera, então os padrões do §2 e as mudanças aparecem.
- **IT-03** — Dados 10.000 usuários com preferências variadas, quando `EnqueueAsync` é chamado duas vezes com a mesma `DedupeKey`, então as entregas são criadas uma vez só, em ≤ 2 s.
- **IT-04** — Dado um servidor de push falso (HTTP) que registra as chamadas, quando 1.000 entregas push são processadas, então cada chamada traz o payload criptografado (aes128gcm) e o cabeçalho `Authorization: vapid …` válido, em ≤ 60 s; uma inscrição que responde 410 é removida; uma que responde 500 é tentada de novo.
- **IT-05** — Dado um aviso por e-mail com o Mailpit real, quando entregue, então o e-mail tem o link de descadastro; `POST /notifications/unsubscribe` com o token desliga aquele tipo; token adulterado dá 400.
- **IT-06** — Dado um fã com inscrições, preferências e entregas, quando o `Users` exporta e depois exclui a conta, então a seção "notifications" aparece no export (sem chaves de criptografia) e tudo do usuário some do schema `notifications`.
- **IT-07** — Dados os testes de arquitetura, quando rodam, então o `Notifications` não referencia nenhum outro módulo, a biblioteca de push só aparece nele e `IRecipientDirectory` está no contrato dele (implementado pelo `Users`).

### 7.4 Testes de Contrato
- **CT-01** — Dado o snapshot `docs/api/openapi.v1.json` e os tipos públicos, quando lidos, então os endpoints e as interfaces do §6 batem, e `(cd web && pnpm api:check)` passa.

### 7.5 Testes E2E
- **E2E-01** — Dada a aplicação completa, no projeto mobile-chrome com a permissão de notificação concedida pelo Playwright: o fã logado abre "Avisos", toca em "Ativar avisos", vê "Avisos ativos neste aparelho", liga "Resumo diário por e-mail" e recarrega: as escolhas continuam. O manifest responde e o service worker registra. Sem violações do axe. [jornada: seguir-e-avisar]
- **E2E-02** — Dada a aplicação completa, no projeto mobile-chrome com a permissão negada: "Ativar avisos" mostra "Os avisos estão bloqueados no navegador."; e abrindo um link de descadastro válido (gerado pelo teste) a página confirma. [jornada: seguir-e-avisar]

### 7.6 Outros
- **Herdado da revisão da SPEC-0028 (G4):**
  - H28-05 — o AppHost do Aspire passa `Support__PixKey`, `Support__PixName`, `Support__PixCity` e `Site__ContactEmail` ao web (valores de desenvolvimento, só do servidor), como o `tools/compose-up.sh`.
  - H28-06 — refactor: o helper de validação de endpoint (`EndpointValidation` do Events, cópias no Profiles e no `SiteEndpoints`) vai para `Common.Presentation`, usado pelo endpoint novo desta spec; os outros módulos migram quando forem tocados.
- **Herdado do CI da SPEC-0026:** H26-09 — `PersonalDataSectionNamesTests.SectionsWithDistinctNamesStartNormally` (SPEC-0021:IT-07) é instável no CI: chama `/health` logo após criar o banco isolado e recebe 503 enquanto as migrações (outbox/inbox) ainda rodam. Esperar o host ficar pronto (ou repetir até um prazo) antes de afirmar o 200.
- Segurança: G4 confere a lista de serviços de push permitidos (SSRF), o segredo VAPID e o token de descadastro.

**Dublês e dados de teste:** servidor de push falso nos IT; chaves VAPID de teste geradas no teste; Mailpit real.

**Ambiente de execução:** IT com Testcontainers (PostgreSQL, Mailpit) e o servidor de push falso em processo; E2E contra a pilha do Compose; Playwright com um worker.

## 8. Plano de Rollout
- **Estratégia:** deploy direto — sem produção até o go-live (SPEC-0003), quando entram as chaves VAPID de produção.
- **Dados/schema:** tabelas novas no schema `notifications` (aditivo).
- **Compatibilidade:** `INotificationsApi` ganha métodos novos sem mudar o `SendEmailAsync` (SPEC-0018@1 continua valendo).
- **Observabilidade:** contadores `subsolo.notifications.deliveries` (por canal e resultado) e `subsolo.notifications.subscriptions_removed`; alerta no Seq se a taxa de falha de push passar de 20% em 1 h.
- **Rollback:** reverter o merge commit; as tabelas podem ficar.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] E-mail de aviso ligado por padrão? — Não; push e e-mail de aviso por consentimento: e-mail desligado até o fã ligar (ADR-0008; recomendação do Architect; piloto automático do Thomas, 2026-10-18)
- [x] Quem decide quem recebe? — O `Engagement`; o `Notifications` só entrega (ADR-0014 esclarecido em 2026-10-18)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

H1 delegado: piloto automático do Thomas até o deploy (2026-10-17, "siga no piloto automático com o recomendado"); tela do Stitch já existente.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Regras**
- [ ] Red: UT-01..UT-03 com a tag `SPEC-0030:<ID>`, falhando pelo motivo certo
- [ ] Green e Refactor

**Fase 2: Módulo, canal e contrato**
- [ ] Red: IT-01..IT-07, CT-01 falhando pelo motivo certo
- [ ] Green: inscrições, preferências, entregas, canal WebPush (dependência nova pelo ADR-0009), `IRecipientDirectory` no `Users`, descadastro, OpenAPI e cliente TS
- [ ] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: PWA, tela e jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [ ] Green: manifest, service worker, tela "Avisos", página de descadastro

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 9ba0f11 | 2026-10-18 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0030`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-20 | `touches` ganha `src/Common/Subsolo.Common.Presentation/**` e `tests/Common/Subsolo.Common.UnitTests/**` para o H28-06 (helper de validação de endpoint em `Common.Presentation`, com teste). | lacuna de escopo vista pelo Architect antes do G1 | — | piloto automático do Thomas (2026-10-17) |
