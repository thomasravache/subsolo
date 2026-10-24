# Runbook — Avisos sem envio

Sinal (SPEC-0030):
- Alerta do Seq **"Falha no envio de aviso"** (`infra/seq/alerts.json`): mais de 20 eventos `NoticeDeliveryFailed` em
  1 hora. O evento (nome do evento de log, `EventId.Name`; no Seq, o atributo `event.name`) é emitido pelo envio de
  entregas do módulo Notifications (`DeliverySender`) uma vez por entrega que termina como falha, em qualquer canal
  (`push` ou `email`): esgotou as tentativas, não tem mais aparelho inscrito ou e-mail, ou não tem link de descadastro.
  A mensagem traz o número da entrega, o canal, o tipo do aviso (`new_show`, `reminder`, `digest`) e as tentativas;
  nunca o endpoint do aparelho, o e-mail ou o conteúdo do aviso.

Enquanto o envio falha, os fãs não recebem os avisos de show novo, o resumo diário ou o lembrete na véspera daquele
canal. Nada mais é afetado: inscrições, preferências e o resto do site seguem funcionando. Uma entrega que terminou
como falha não é reenviada sozinha (o aviso perdeu a hora).

## 1. Confirmar
1. No Seq, filtre `event.name = 'NoticeDeliveryFailed'` e veja o `Channel` e o `Kind` dos eventos mais recentes: é um
   canal só ou os dois? Um tipo de aviso só?
2. Veja a métrica `subsolo.notifications.deliveries` por `subsolo.notifications.channel` e
   `subsolo.notifications.result` (`sent`, `retry`, `gone`, `failed`): muito `retry` antes das falhas aponta para o
   serviço fora do ar; muito `gone` (e `subsolo.notifications.subscriptions_removed` subindo) aponta para inscrições
   mortas.
3. Procure a causa nos eventos do canal, no mesmo intervalo:
   - push: `event.name = 'WebPushRefused'` (o serviço de push respondeu com erro; a mensagem traz o serviço e o
     código HTTP), `'WebPushUnreachable'` (sem resposta do serviço de push) e `'WebPushNotConfigured'` (sem chaves
     VAPID);
   - e-mail: `event.name = 'EmailUnavailable'` (SMTP fora do ar, lento ou recusando) e `'UnsubscribeKeyMissing'`
     (sem a chave do link de descadastro, nenhum e-mail de aviso sai);
   - os dois: `event.name = 'NoticeSendError'` (exceção inesperada num envio, com a pilha) e
     `'NoticeDeliveriesRunFailed'` (o job de entregas falhou inteiro, por exemplo com o banco fora).

## 2. Diagnosticar
- **`WebPushNotConfigured`**: faltam `Notifications:WebPush:*` (chave pública, privada e `Subject`). Todo push falha.
- **`WebPushRefused` com 401/403**: a assinatura VAPID foi recusada — a chave privada não corresponde à pública que os
  navegadores usaram para se inscrever (chaves trocadas depois das inscrições) ou o `Subject` é inválido.
- **`WebPushRefused` com 404/410**: o aparelho cancelou a inscrição; a inscrição é removida e o fã sem outro aparelho
  fica sem push. Esperado em pequena quantidade; em massa, veja se o domínio ou o service worker (`/sw.js`) mudou.
- **`WebPushUnreachable` ou `WebPushRefused` com 429/5xx**: o serviço de push (FCM, Mozilla, Apple) está fora do ar
  ou limitando; cada entrega tenta 6 vezes com espera crescente (2 a 32 s) antes de desistir.
- **`EmailUnavailable`**: o servidor SMTP está fora do ar, lento ou recusando. Local: o Mailpit do Compose parou
  (`docker compose ps mailpit`). Produção: veja o status do provedor de e-mail e as credenciais
  (`Notifications:Smtp:*`).
- **`UnsubscribeKeyMissing`**: falta `Notifications:Unsubscribe:SigningKey` (segredo).
- **`NoticeSendError`**: trate como bug — a pilha mostra onde.

## 3. Agir
- Configuração errada (VAPID, SMTP, chave de descadastro): corrija o segredo ou a configuração e reinicie a API. Não
  troque o par de chaves VAPID para "consertar": as inscrições existentes passam a ser recusadas e cada fã precisa
  ativar os avisos de novo.
- Serviço externo fora do ar: restabeleça (local: `docker compose start mailpit`) ou espere o provedor; as entregas
  ainda pendentes saem sozinhas, sem reiniciar a API.
- Bug: corrija com uma spec `fix` (teste de regressão primeiro).

## 4. Encerrar
Os eventos `NoticeDeliveryFailed` param e `subsolo.notifications.deliveries` volta a ter `sent` nos dois canais.
Registre a causa.
