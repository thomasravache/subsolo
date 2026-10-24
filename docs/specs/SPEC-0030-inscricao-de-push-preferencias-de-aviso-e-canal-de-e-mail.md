---
id: SPEC-0030
title: Inscrição de push, preferências de aviso e canal de e-mail
tier: full
type: feature
user_facing: true
status: implemented
created: 2026-10-18
parent: SPEC-0002
depends_on: [SPEC-0019, SPEC-0027]
consumes_contract: []
contract_version: 3
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: [src/Modules/Notifications/**, tests/Modules/Notifications/**, src/Modules/Users/**, tests/Modules/Users/**, src/Subsolo.Api/Program.cs, src/Subsolo.Api/appsettings.json, src/Subsolo.Api/appsettings.Development.json, src/Subsolo.Api/packages.lock.json, src/Subsolo.AppHost/**, Directory.Packages.props, tests/Subsolo.ArchitectureTests/**, tests/**/packages.lock.json, docs/api/**, web/src/api/**, web/src/app/(public)/seguindo/**, web/src/app/(public)/avisos/**, web/src/app/manifest.ts, web/public/sw.js, web/public/icons/**, web/src/components/notifications/**, web/src/proxy.ts, web/src/proxy.test.ts, tools/compose-up.sh, e2e/tests/avisos/**, docs/design/**, src/Common/Subsolo.Common.Presentation/**, tests/Common/Subsolo.Common.UnitTests/**, tests/Common/Subsolo.AppHost.IntegrationTests/**, infra/seq/alerts.json, docs/runbooks/**, tests/Observability/Subsolo.Observability.UnitTests/**]
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
| Payload do push | título e corpo no tamanho máximo, com acentos | JSON sem escapes `\uXXXX` de letras acentuadas; cabe no limite de 4 KB do Web Push | UT-04 |
| Falha inesperada no lote | o canal lança exceção numa entrega | só ela volta para nova tentativa (`attempts` +1); as outras do lote ficam gravadas e não saem de novo | IT-08 |
| Painel fora de sincronia | o navegador tem inscrição que o servidor não conhece; permissão já negada ao abrir | a inscrição é registrada de novo ao abrir; bloqueio mostrado sem precisar tocar; "Ativando…" durante a ativação | UT-05 |
| Alerta | entregas que terminam em falha | alerta "Falha no envio de aviso" no Seq com runbook | IT-09 |

## 7. Artefato B — Plano de Testes (TDD)

### 7.1 Testes de Caracterização
- N/A — o canal de e-mail e o `SendEmailAsync` da SPEC-0018 seguem cobertos pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dado o validador de inscrição, quando recebe endpoints `https://fcm.googleapis.com/…`, `https://updates.push.services.mozilla.com/…`, `https://web.push.apple.com/…`, `http://…`, `https://evil.com/…` e chaves ausentes, então só os três primeiros com chaves passam.
- **UT-02** — Dadas preferências variadas e um `Notice` de show novo, quando o planejador de entregas roda, então cria entregas só para os canais permitidos por usuário e ignora chaves já entregues.
- **UT-03** — Dado o componente "Avisos" (Vitest) com `Notification.permission` em `default`, `granted`, `denied` e sem suporte, e com `navigator.standalone` falso no iOS, então mostra o botão, os interruptores ou a mensagem certa, e só pede permissão depois do toque.

- **UT-04** — Dado o montador do payload do push, quando recebe título de 120 e corpo de 1.000 caracteres (os máximos que o `EnqueueAsync` aceita, `NoticeQueue.MaxTitleLength`/`MaxBodyLength`) feitos só de letras acentuadas e uma URL de 200 caracteres, então o JSON `{title, body, url}` não escapa as letras (`\u00e1` etc.) e, criptografado (aes128gcm), cabe em 4.096 bytes.
- **UT-05** — Dado o componente "Avisos" (Vitest): com uma inscrição no navegador, ao abrir ele a registra de novo no servidor (`POST`, idempotente) antes de mostrar "Avisos ativos neste aparelho"; com a permissão já `denied` (Permissions API ou `Notification.permission`) ao abrir, mostra "Os avisos estão bloqueados no navegador." sem o botão; durante a ativação mostra o texto `activating` do §6 e desabilita o botão.

### 7.3 Testes de Integração
- **IT-01** — Dados a API e o PostgreSQL, quando o fã registra uma inscrição válida duas vezes e uma inválida, então 204 (um registro) e 400; `DELETE` remove.
- **IT-02** — Dado um fã, quando lê as preferências antes e depois de ativar o push e as altera, então os padrões do §2 e as mudanças aparecem.
- **IT-03** — Dados 10.000 usuários com preferências variadas, quando `EnqueueAsync` é chamado duas vezes com a mesma `DedupeKey`, então as entregas são criadas uma vez só, em ≤ 2 s.
- **IT-04** — Dado um servidor de push falso (HTTP) que registra as chamadas, quando 1.000 entregas push são processadas, então cada chamada traz o payload criptografado (aes128gcm) e o cabeçalho `Authorization: vapid …` válido, em ≤ 60 s; uma inscrição que responde 410 é removida; uma que responde 500 é tentada de novo.
- **IT-05** — Dado um aviso por e-mail com o Mailpit real, quando entregue, então o e-mail tem o link de descadastro; `POST /notifications/unsubscribe` com o token desliga aquele tipo; token adulterado dá 400.
- **IT-06** — Dado um fã com inscrições, preferências e entregas, quando o `Users` exporta e depois exclui a conta, então a seção "notifications" aparece no export (sem chaves de criptografia) e tudo do usuário some do schema `notifications`.
- **IT-07** — Dados os testes de arquitetura, quando rodam, então o `Notifications` não referencia nenhum outro módulo, a biblioteca de push só aparece nele e `IRecipientDirectory` está no contrato dele (implementado pelo `Users`).

- **IT-08** — Dado um lote de entregas push e e-mail em que um canal lança uma exceção inesperada numa delas, quando o lote é processado, então só essa entrega fica pendente com `attempts` +1 e próxima tentativa com espera; as outras ficam `sent`; processar o lote de novo não reenvia nenhuma já enviada (o resultado parcial é gravado mesmo com cancelamento).
- **IT-09** — Dado `infra/seq/alerts.json`, então existe o alerta "Falha no envio de aviso" que seleciona `NoticeDeliveryFailed` por igualdade em `event.name` (sem texto da mensagem), janela de 1 h, `count > 20`, citando um runbook de `docs/runbooks/` que existe — no padrão de `MagicLinkSendFailedAlertTests` da SPEC-0016 (`tests/Observability/Subsolo.Observability.UnitTests`).

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
- **Observabilidade:** contadores `subsolo.notifications.deliveries` (por canal e resultado) e `subsolo.notifications.subscriptions_removed`; alerta no Seq "Falha no envio de aviso": `event.name = 'NoticeDeliveryFailed'` (um evento por entrega que termina como falha, qualquer canal), janela de 1 h, `count > 20`, com o runbook `docs/runbooks/avisos-sem-envio.md` (IT-09). A configuração do Seq como código (SPEC-0009) só dispara por contagem, por isso o limite é absoluto e não uma taxa.
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
- [x] Red: UT-01..UT-03 com a tag `SPEC-0030:<ID>`, falhando pelo motivo certo
- [x] Green e Refactor

**Fase 2: Módulo, canal e contrato**
- [x] Red: IT-01..IT-07, CT-01 falhando pelo motivo certo
- [x] Green: inscrições, preferências, entregas, canal WebPush (dependência nova pelo ADR-0009), `IRecipientDirectory` no `Users`, descadastro, OpenAPI e cliente TS
- [x] Validar: build + suíte + arquitetura (G2/G3)

**Fase 3: PWA, tela e jornada E2E**
- [x] Red: E2E-01, E2E-02 falhando pelo motivo certo
- [x] Green: manifest, service worker, tela "Avisos", página de descadastro

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy: N/A até o go-live — G6 = N/A apontando para SPEC-0003
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 9ba0f11 | 2026-10-18 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" --max-parallel-test-modules 1 && (cd web && pnpm test)` exit 2 (red: Test run completed with non-success exit code: 2 (see: https://aka.ms/testingplatform/exit) — 0145e1a | 2026-10-23 |
| G2 Green | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); lint exit 0 ($ tsc --noEmit); coverage exit 0 (coverage: relatórios em coverage/ (index.html, Summary.txt, Cobertura.xml, changed-lines.m) — 6ff18c1 | 2026-10-23 |
| G3 Arquitetura | PASS | arch_test exit 0 (duration: 9s 280ms) — 6ff18c1 | 2026-10-23 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINOR ITEMS (re-revisão após a Emenda 3: o major do alerta resolvido em e48efca; achados 2, 3, 6 e 7 resolvidos; 0 bloqueadores/majors; aviso dos lockfiles de ab7a0d8 aceito como regeneração; H30-01..07 levados à SPEC-0031 §7.6; chave de descadastro na lista da SPEC-0003) — 0b6633e | 2026-10-23 |
| G5 Integração & CI | PASS | build exit 0 ($ next build); test exit 0 (Set `VITE_CONFIG_NATIVE_IGNORE_WARNING=true` to suppress this warning.); test_integration exit 0 (duration: 14m 24s 928ms); test_e2e exit 0 ([WebServer]  Network subsolo-local_aspire Removed); arch_test exit 0 (duration: 9s 436ms) — 63fa1eb | 2026-10-23 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-17); G5 PASS | 2026-10-23 |
| G6 Deploy | N/A | sem ambiente implantado até o go-live — SPEC-0003 (deploy guiado pelo Thomas) | 2026-10-23 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — e62b292 (árvore suja) | 2026-10-23 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-23 | G2 | falha | 3 FAILs seguidos no G2: o 1º pelo aviso de segurança do Next.js 16.4.0 (resolvido pela SPEC-0034); o 2º e o 3º na cobertura, porque NoticesModuleRulesTests.TheNewEndpointValidatesWithTheCommonEndpointValidation (H28-06) falha no build Release usado pelo coverage.sh: a chamada a EndpointValidation fica na máquina de estados async, que no Release é struct e o ArchUnitNET não enxerga; no Debug passa | rodar o Tooling.Tests sozinho (passa); log completo do coverage.sh (falha só esse teste de arquitetura, 78/79) | Architect (piloto automático do Thomas) | TEST_DEFECT corrigido pelo Test-writer em 5bfdbf8: o teste H28-06 lê o uso do EndpointValidation pelos metadados (MemberRef) do assembly, igual em Debug e Release (79/79 nos dois; falha nos dois sem a chamada) | 2026-10-23 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
- Parte "Avisos" em `/seguindo`: botão "Ativar avisos", aviso de bloqueio ou de navegador sem suporte, orientação para o iPhone e quatro interruptores (show novo por push, resumo diário por e-mail, lembrete por push e por e-mail).
- Inscrição de push com Web Push (VAPID) direto, sem serviço pago: manifest, service worker `/sw.js`, `GET /api/v1/push/public-key`, `POST`/`DELETE /api/v1/me/push-subscriptions` e preferências em `GET`/`PUT /api/v1/me/notification-preferences`.
- Fila de entregas por canal (`EnqueueAsync`), enviada por um job a cada segundo, com novas tentativas (2 a 32 s, até 6), remoção de inscrição morta (404/410) e nenhum aviso enviado duas vezes.
- Canal de e-mail com link de descadastro assinado (HMAC) e página `/avisos/descadastro`.
- Export e exclusão de conta cobrem os dados de avisos (sem chaves de criptografia); contadores `subsolo.notifications.deliveries` e `subsolo.notifications.subscriptions_removed`; alerta "Falha no envio de aviso" com o runbook `docs/runbooks/avisos-sem-envio.md`.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (scaffold 60b688c, 07c7801; testes b6e7f27, 6e396f0, 2f31d56, 93c4c2e, c7fd28e, 3bf782f, 80803a5, dab9280, 615f197, 0145e1a, efd38cc; Emenda 3: 55fa928, cdda312, 0b41fcd, 42a5b9b, 5d469b0, 5bfdbf8) → Implementer (8589265, ab7a0d8, 0302d83, cf2c1ed, 4ca09a3, cefa08d, f0cea59, 292f602; Emenda 3: 92ce877, e48efca, 90eb664, bd67e4e) → Reviewer: CHANGES REQUESTED (alerta ausente) e, depois da Emenda 3, APPROVED WITH MINOR ITEMS.
- Módulo `Notifications` com tabelas `subscriptions`, `preferences`, `notices` e `deliveries`; `Lib.Net.Http.WebPush` 3.3.1 (MIT, versão fixa pelo ADR-0009); `IRecipientDirectory` no contrato, implementado pelo `Users`; SSRF evitado com lista de hosts exatos.
- Chaves VAPID e de descadastro de desenvolvimento geradas pelo AppHost e pelo `tools/compose-up.sh`; nenhum segredo no repositório nem variável exposta ao navegador; CSP ganha `worker-src 'self'`.
- Emendas sem mudança de contrato: escopo do helper comum, textos e formato do token, e a Emenda 3 da revisão (alerta por contagem, lote resiliente, payload sem escapes, painel sincronizado). IMP-01 (três falhas do G2: Next.js 16.4.0 e o teste de arquitetura no build Release) resolvido.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | só endpoints `https` do FCM, Mozilla e Apple com chaves passam no validador de inscrição | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| UT-02 | o planejador cria entregas só nos canais permitidos por usuário e ignora chaves já entregues | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| UT-03 | painel "Avisos" com permissão `default`, `granted`, `denied`, sem suporte e iPhone fora da tela inicial; permissão pedida só depois do toque | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| UT-04 | payload `{title, body, url}` sem escapes de letras acentuadas; título de 120 e corpo de 1.000 caracteres cabem em 4.096 bytes cifrados | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| UT-05 | painel registra de novo a inscrição do navegador ao abrir, mostra o bloqueio ao abrir sem o botão e "Ativando…" com o botão desabilitado | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-01 | inscrição válida duas vezes → 204 e um registro; inválida → 400 `notifications.invalid_subscription`; `DELETE` remove | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-02 | preferências padrão (e-mail desligado, push de show novo ligado na primeira inscrição) e alterações salvas | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-03 | 10.000 destinatários com a mesma `DedupeKey` duas vezes: entregas criadas uma vez só, em ≤ 2 s | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-04 | 1.000 entregas push com aes128gcm e `Authorization: vapid` válidos em ≤ 60 s; 410 remove a inscrição; 500 tenta de novo | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-05 | e-mail de aviso no Mailpit com link de descadastro; o token desliga aquele tipo; token adulterado → 400 `notifications.invalid_token` | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-06 | export com a seção "notifications" sem chaves de criptografia; exclusão apaga inscrições, preferências e entregas | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-07 | `Notifications` sem dependência de módulo de domínio, biblioteca de push só nele, `IRecipientDirectory` no contrato dele e implementado pelo `Users` | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-08 | exceção inesperada numa entrega ou cancelamento do lote: só ela fica pendente, as outras ficam `sent` e nada sai duas vezes | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| IT-09 | alerta "Falha no envio de aviso" no Seq (`NoticeDeliveryFailed`, 1 h, `count > 20`) com runbook existente | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| CT-01 | OpenAPI e interfaces públicas como no §6; `pnpm api:check` passa | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| E2E-01 | fã ativa os avisos, vê "Avisos ativos neste aparelho", liga o resumo diário por e-mail e a escolha continua depois de recarregar; manifest e service worker; axe sem violações (mobile-chrome) | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| E2E-02 | permissão negada mostra "Os avisos estão bloqueados no navegador."; link de descadastro válido confirma (mobile-chrome) | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| H28-05 | o AppHost passa as variáveis do Apoie e o e-mail de contato ao web, só no servidor | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| H28-06 | `EndpointValidation` em `Common.Presentation`, usado pelo endpoint de inscrição (teste lê o uso pelos metadados, igual em Debug e Release) | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |
| H26-09 | `PersonalDataSectionNamesTests` espera o host ficar pronto antes de afirmar o 200 | PASS | `gate G2 --run` em 6ff18c1 (cobertura de 97% das linhas alteradas); `gate G5 --run` na integração (63fa1eb); CI do PR #42 (run 40051799815, checks verdes em e62b292) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — CI verde no PR #42 (run 40051799815)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — G6 N/A até a SPEC-0003
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7) — CHANGELOG atualizado
- [x] Pendências registradas como novas specs (ou nenhuma) — ver Pendências

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente implantado (G6 N/A → SPEC-0003, guiado pelo Thomas). Entrega na `main` pelo PR #42 (https://github.com/thomasravache/subsolo/pull/42), merge commit 3e4d99f em 2026-10-23, com H2 pela política auto-on-green.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0031 §7.6: H30-01 (WNS do Edge no Windows), H30-02 (retenção de entregas e avisos), H30-03 (campos obrigatórios no PUT de preferências), H30-04 (`List-Unsubscribe-Post`), H30-05 (alerta para o job inteiro parado), H30-06 (evento próprio para "sem canal do destinatário"), H30-07 (reenvio da inscrição não religa o push de show novo).
- SPEC-0003: chaves VAPID e chave de assinatura do descadastro de produção como segredos do environment.
## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0030`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (sem mudança de contrato) | 2026-10-20 | `touches` ganha `src/Common/Subsolo.Common.Presentation/**` e `tests/Common/Subsolo.Common.UnitTests/**` para o H28-06 (helper de validação de endpoint em `Common.Presentation`, com teste). | lacuna de escopo vista pelo Architect antes do G1 | — | piloto automático do Thomas (2026-10-17) |
| 1 (sem mudança de contrato) | 2026-10-23 | `touches` ganha `tests/Common/Subsolo.AppHost.IntegrationTests/**` (teste do H28-05 e das chaves de desenvolvimento passadas pelo AppHost). Textos: sem suporte → "Este navegador não recebe avisos."; descadastro → "Pronto: você não recebe mais o resumo diário por e-mail." (`digest`) ou "Pronto: você não recebe mais o lembrete na véspera por e-mail." (`reminder`); token inválido → "Este link de descadastro é inválido.". Token de descadastro: `base64url(JSON {"u":"<userId>","k":"digest"|"reminder"}).base64url(HMAC-SHA256)`, chave `Notifications:Unsubscribe:SigningKey` (segredo; valor de teste só na pilha local, `Notifications__Unsubscribe__SigningKey`), e a página lê o tipo do próprio token. Esclarecimentos: `newShowPush` falso até a primeira inscrição, que o liga; `deliveries.channel` `push`/`email`; payload do push `{title, body, url}`; sem inscrição push ou sem e-mail não há entrega daquele canal; a parte "Avisos" fica em `/seguindo`, numa região "Avisos"; os quatro interruptores do §6. | SPEC_DEFECTs do Test-writer no G1 | — | piloto automático do Thomas (2026-10-23) |
| 1 (sem mudança de contrato) | 2026-10-23 | Revisão G4 (CHANGES REQUESTED): `touches` ganha `infra/seq/alerts.json`, `docs/runbooks/**` e `tests/Observability/Subsolo.Observability.UnitTests/**`; o alerta do §8 vira "Falha no envio de aviso" (`NoticeDeliveryFailed`, 1 h, `count > 20`, runbook `avisos-sem-envio.md`), porque o Seq como código só dispara por contagem; novos UT-04 (payload sem escapes de acentos, cabe em 4 KB), UT-05 (painel registra de novo a inscrição do navegador, mostra o bloqueio ao abrir e o texto `activating`) e IT-08 (exceção inesperada numa entrega não descarta o lote nem reenvia — §5 "um aviso nunca sai duas vezes"), IT-09 (alerta). Aceitos sem mudança: descadastro ao abrir a página (previsto no §6); WNS (Edge no Windows) fora da lista do §5. Levados adiante: H30-01..04 na SPEC-0031 §7.6; chave de descadastro de produção no checklist da SPEC-0003. | achados 1–3, 6 e 7 do Reviewer independente (G4) | — | piloto automático do Thomas (2026-10-23) |
| 2 | 2026-10-23 | Pela SPEC-0031 (Emenda 1): os quatro campos do `PUT /api/v1/me/notification-preferences` passam a ser obrigatórios (400 `notifications.invalid_preferences`); novo `POST /api/v1/notifications/unsubscribe/one-click?token=…` (RFC 8058, sem X-CSRF) e cabeçalhos `List-Unsubscribe`/`List-Unsubscribe-Post` nos e-mails de aviso. Implementado e testado na SPEC-0031. | achados H30-03 e H30-04 do G4 | SPEC-0031 | piloto automático do Thomas (2026-10-23) |
| 3 | 2026-10-24 | Pela SPEC-0031 (Emenda 3): `Notice.Url` aceita caminho do site começando com `/` (resolvido contra `App:PublicUrl` no e-mail); `deliveries.status` ganha `skipped` para entrega sem canal do destinatário. | SPEC_DEFECTs 3 e 8 do G1 da SPEC-0031 | SPEC-0031 | piloto automático do Thomas (2026-10-24) |
