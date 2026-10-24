# Runbook — Outbox parada

Sinais (SPEC-0009):
- **`/health` → `entries.outbox` Unhealthy**: o job `ProcessOutbox` não executa há mais de 60 s
  (`OutboxHealthCheck`, alimentado por um listener do Quartz).
- Alerta do Seq **"Falha de job"** (`infra/seq/alerts.json`): 3 ou mais eventos `OutboxProcessingFailed` em
  10 minutos. O evento (nome do evento de log, `EventId.Name`; no Seq, o atributo `event.name`) é emitido, com a
  exceção, quando:
  - uma mensagem da **outbox** ou da **inbox** falha ao ser processada (handler lançou, tipo de evento desconhecido)
    — o job agenda nova tentativa com backoff, então uma mensagem que falha sempre gera um evento por tentativa;
  - o job **inteiro** (`<Módulo>.ProcessOutbox` ou `<Módulo>.ProcessInbox`) termina com exceção — tipicamente o
    PostgreSQL fora do ar (um evento a cada execução, a cada 2 s).

- O mesmo alerta conta também as falhas inteiras dos jobs de avisos (SPEC-0031), que entram na soma de 3 em 10 minutos:
  - `NoticeDeliveriesRunFailed`: o job `Notifications.ProcessDeliveries` (entregas de avisos, a cada 1 s) falhou
    inteiro — tipicamente o PostgreSQL fora do ar; as entregas pendentes saem quando ele volta;
  - `DailyDigestRunFailed`: o job `Engagement.DailyDigest` (resumo diário por e-mail, às 9h de São Paulo,
    `Engagement:DigestCron`) falhou. Como ele roda uma vez por dia, tem também o alerta próprio abaixo.
- Alerta do Seq **"Resumo diário falhou"** (`infra/seq/alerts.json`, SPEC-0031): 1 evento `DailyDigestRunFailed` em
  1 hora basta, porque o job roda uma vez por dia.

Enquanto a outbox está parada, os eventos de domínio ficam gravados (nada se perde), mas os efeitos assíncronos
(avisos, integrações entre módulos) atrasam.

## 1. Confirmar
1. `GET /health` da API: veja `entries.outbox` e `entries.postgres`.
2. No Seq, filtre `event.name = 'OutboxProcessingFailed'` e abra a exceção do evento mais recente: a
   mensagem diz se foi uma mensagem ("Falha ao processar ... (tentativa N)") ou o job inteiro.
3. Veja os spans `ProcessOutbox <Evento>` (fonte `Subsolo.Messaging`) com status de erro: a tag
   `subsolo.message.id` identifica a mensagem.
4. Avisos (SPEC-0031): filtre `event.name = 'NoticeDeliveriesRunFailed' or event.name = 'DailyDigestRunFailed'` e
   abra a exceção. Para as entregas de avisos em si (não o job inteiro), veja `docs/runbooks/avisos-sem-envio.md`.

## 2. Diagnosticar
- **PostgreSQL fora do ar** (`entries.postgres` Unhealthy, exceções do Npgsql): é a causa mais comum; trate o banco
  primeiro (`docs/runbooks/erros-5xx.md`, item 3). O job volta sozinho quando o banco volta.
- **Job não executa e não falha** (sem `OutboxProcessingFailed`, mas `entries.outbox` Unhealthy): o scheduler do
  Quartz parou ou está travado numa execução longa — veja os logs da categoria `Quartz` e reinicie a API.
- **Uma mensagem falhando sempre** (erro por mensagem, com novas tentativas): veja as colunas `error` e `attempts` da tabela
  `outbox_messages` do schema do módulo; o handler que falha está no log "Falha ao processar ...".

### Resumo diário falhou
1. No Seq, filtre `event.name = 'DailyDigestRunFailed'` e abra a exceção: banco fora do ar (`entries.postgres` do
   `/health`) é a causa mais comum; erro do Notifications ou de configuração (`Engagement:DigestCron` inválido impede a
   API de subir) aparece na pilha.
2. Veja se o resumo do dia saiu para parte dos fãs: `select count(*) from notifications.deliveries where dedupe_key =
   'digest:<aaaa-mm-dd>'`.

## 3. Agir
- Reiniciar a API (`docker compose restart api`) depois de corrigir a causa; o job retoma as mensagens pendentes.
- Resumo diário que falhou (`DailyDigestRunFailed`): corrigida a causa, ele roda de novo na próxima execução do
  `Engagement:DigestCron` (o dia seguinte). Os shows publicados ficam 7 dias em `engagement.published_shows` e o resumo
  olha as últimas 24 h; rodar de novo no mesmo dia não duplica e-mail (chave `digest:{data}` por fã).
- Bug num handler: corrija com uma spec `fix` (teste de regressão primeiro); as mensagens com falha são
  reprocessadas na próxima tentativa (backoff), sem duplicar handlers já executados.

## 4. Encerrar
`/health` volta a 200 com `entries.outbox` Healthy e os eventos `OutboxProcessingFailed` (e `NoticeDeliveriesRunFailed`,
`DailyDigestRunFailed`) param. Registre a causa.
