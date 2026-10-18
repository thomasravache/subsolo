---
id: ADR-0014
title: Avisos por Web Push e e-mail
status: accepted
origin: decision
date: 2026-10-13
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: teste de arquitetura (canais só implementam INotificationChannel no módulo Notifications) + testes de integração com Mailpit e servidor de push falso
---

# ADR-0014 — Avisos por Web Push e e-mail

## Contexto e Problema
O fã segue cidades, bandas e casas e precisa ser avisado quando sai um show novo, e lembrado na véspera de quem marcou "vou". O Thomas escolheu **push no navegador + e-mail** no lançamento, com o **WhatsApp** entrando depois como mais um canal, sem refazer nada. E-mail também transporta o link mágico de login (ADR-0003).

## Direcionadores da Decisão
- Canais do lançamento: Web Push (PWA) + e-mail (escolha do usuário).
- WhatsApp (e outros) depois, como um canal plugável.
- Nenhum custo até o go-live (ADR-0012): localmente, e-mail num servidor de teste.
- Não lotar a caixa do fã: show novo por e-mail sai em resumo diário.

## Opções Consideradas
- Módulo `Notifications` com canais plugáveis: Web Push (VAPID, biblioteca `WebPush`) + e-mail via **SMTP padrão** (Mailpit no local; o provedor de produção é escolhido no go-live)
- E-mail via SDK do provedor (Amazon SES/Resend) desde já
- Serviço de notificação terceirizado (OneSignal, Firebase Cloud Messaging)

## Resultado da Decisão
**Opção escolhida:** canais plugáveis no módulo `Notifications`, com e-mail via SMTP padrão. Trocar Mailpit → provedor real vira configuração, sem código e sem custo agora. A recomendação para o go-live é o Amazon SES (US$ 0,10 por 1.000), que também aceita SMTP. O Web Push usa VAPID direto, sem intermediário e sem custo.

- **Contrato interno:** `INotificationChannel { Channel; SendAsync(Notification) }`. Os canais `WebPush` e `Email` entram no lançamento; `WhatsApp` entra depois, implementando a mesma interface.
- **Disparo:** os módulos `Events` e `Engagement` publicam eventos de integração (`ShowPublished`, `ShowStatusChanged`, `AttendanceMarked`); o `Notifications` resolve quem deve receber (seguidores da cidade, da banda ou da casa) e enfileira os avisos (outbox).
- **Esclarecimento de 2026-10-18 (direção das dependências, ADR-0001):** quem decide **quem** recebe é o `Engagement` (consome `ShowPublished`/`ShowChanged` do `Events`, cruza com seguidores e "vou", e roda o job do lembrete de véspera); o `Notifications` só **entrega** pelos canais, a partir de `INotificationsApi.EnqueueAsync(destinatários, aviso)`. O `Notifications` não depende de nenhum módulo de domínio: o e-mail do destinatário vem de `IRecipientDirectory`, definido no contrato do `Notifications` e implementado pelo `Users`; exclusão de conta e export chegam pelo `INotificationsApi` chamado pelo `Users`. Assim não há o ciclo `Users → Notifications → Engagement → Profiles → Users`.
- **Show novo:** push imediato; por e-mail, resumo diário (no máximo um por dia).
- **Lembrete de véspera:** job diário do Quartz às 10h, no fuso da cidade, para quem marcou "vou"; shows cancelados ou adiados ficam de fora.
- **Idempotência:** chave única (usuário + show + tipo); um aviso nunca sai duas vezes (ADR-0011).
- **Preferências:** o fã escolhe canais e tipos de aviso; cada e-mail tem link de descadastro.
- **Inscrições de push inválidas** (HTTP 404/410) são removidas automaticamente.

### Consequências
- **Boa**, porque o WhatsApp entra como um novo canal sem mudar `Events` nem `Engagement`, e o provedor de e-mail é trocado sem código.
- **Ruim**, porque o push no iPhone só funciona com o site na tela de início, então o e-mail continua necessário; e SMTP é um pouco menos observável do que o SDK do provedor (mitigação: webhooks de bounce no go-live).

### Confirmação (G3)
Teste de arquitetura: só o módulo `Notifications` referencia bibliotecas de push e SMTP. Testes de integração com Mailpit (container) e com um endpoint de push falso; teste de idempotência do lembrete.

## Prós e Contras das Opções
| Critério (peso) | Canais plugáveis + SMTP | SDK do provedor | OneSignal/FCM |
|---|---|---|---|
| Custo antes do go-live (3) | zero | zero (precisa de conta) | zero até a cota |
| Troca de provedor sem código (3) | sim | não | não |
| Dados do fã fora da casa (2) | não | não | sim |
| WhatsApp depois (2) | novo canal | novo canal | depende do serviço |

## Mais Informações
- WebPush (NuGet) 1.0.13, publicado em 2026-04-28; Lib.Net.Http.WebPush 3.3.1, publicado em 2025-03-09 — nuget.org (verificado em 2026-10-13).
- Amazon SES: US$ 0,10 por 1.000 e-mails; aceita SMTP — https://aws.amazon.com/ses/pricing/ (verificado em 2026-10-13).
- Mailpit (MIT), servidor SMTP de teste com interface web — https://mailpit.axllent.org (não reverificado nesta data).
- Padrão Web Push: RFC 8030 e RFC 8292 (VAPID).
