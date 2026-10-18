# Runbook — Link mágico sem envio

Sinal (SPEC-0016):
- Alerta do Seq **"Falha no envio do link"** (`infra/seq/alerts.json`): mais de 5 eventos `MagicLinkSendFailed` em
  10 minutos. O evento (nome do evento de log, `EventId.Name`; no Seq, o atributo `event.name`) é emitido pelo
  `RequestMagicLink` do módulo Users quando o módulo Notifications não manda o e-mail do link; a mensagem traz o
  código do erro (`notifications.email_unavailable`, `notifications.invalid_data` ou `notifications.invalid_recipient`),
  nunca o e-mail nem o token.

Enquanto o envio falha, quem pede o link vê "Não conseguimos mandar o e-mail agora. Tente de novo em alguns minutos."
(503) e ninguém consegue entrar por link mágico. As contas e as sessões abertas não são afetadas.

## 1. Confirmar
1. No Seq, filtre `event.name = 'MagicLinkSendFailed'` e veja o `ErrorCode` dos eventos mais recentes.
2. Veja a métrica `subsolo.users.magic_links_requested` com `subsolo.users.result = failed` e, no módulo
   Notifications, `subsolo.notifications.emails` com falha.
3. Para `notifications.email_unavailable`, filtre também `event.name = 'EmailUnavailable'` (Notifications): a exceção
   mostra se o SMTP recusou a conexão, a autenticação ou a mensagem, ou passou de 10 s.

## 2. Diagnosticar
- **`notifications.email_unavailable`** (o mais comum): o servidor SMTP está fora do ar, lento ou recusando. Local: o
  Mailpit do Compose parou (`docker compose ps mailpit`). Produção: veja o status do provedor de e-mail e as
  credenciais (`Notifications:Smtp:*`).
- **`notifications.invalid_data`**: o link montado não é aceito pelo modelo `MagicLink` — confira `App:PublicUrl`
  (a API valida o formato na inicialização, mas um domínio errado passa na validação).
- **`notifications.invalid_recipient`**: não deveria acontecer (o e-mail é validado antes); trate como bug.

## 3. Agir
- SMTP fora do ar: restabeleça o serviço (local: `docker compose start mailpit`); os pedidos seguintes voltam a
  enviar sem reiniciar a API. Quem pediu durante a falha pede de novo (o link não é reenviado sozinho).
- Configuração errada: corrija `Notifications:Smtp:*` ou `App:PublicUrl` e reinicie a API.
- Bug: corrija com uma spec `fix` (teste de regressão primeiro).

## 4. Encerrar
Os eventos `MagicLinkSendFailed` param e `subsolo.users.magic_links_requested` volta a ter `sent`. Registre a causa.
