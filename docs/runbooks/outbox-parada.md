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
    `Engagement:DigestCron`, ou o gatilho de recuperação `Engagement:DigestCatchUpCron`, a cada 15 min) falhou. Entra
    também no alerta "Job agendado falhou" abaixo.
  - `NoticesRetentionRunFailed` (SPEC-0032): o job `Notifications.NoticesRetention` (limpeza diária das entregas e
    avisos com mais de 90 dias) falhou inteiro — tipicamente o PostgreSQL fora do ar; a próxima execução (no dia
    seguinte) faz a limpeza inteira, nada se perde.
  - `DayBeforeReminderRunFailed` (SPEC-0032): o job `Engagement.DayBeforeReminder` (lembrete na véspera, de hora em hora,
    `Engagement:ReminderCron`) falhou inteiro; a execução da hora seguinte tenta de novo e a chave
    `reminder:{show}:{início}` por fã evita lembrete duplicado.
- Alerta do Seq **"Job agendado falhou"** (`infra/seq/alerts.json`, SPEC-0031; renomeado na SPEC-0036): 1 evento
  `DailyDigestRunFailed`, `DayBeforeReminderRunFailed` ou `NoticesRetentionRunFailed` em 1 hora basta, porque esses
  jobs rodam poucas vezes (o resumo às 9h, com a recuperação a cada 15 min; o lembrete de hora em hora; a retenção
  diária) e o "Falha de job" (3 em 10 minutos) fica para os jobs de alta frequência.

Enquanto a outbox está parada, os eventos de domínio ficam gravados (nada se perde), mas os efeitos assíncronos
(avisos, integrações entre módulos) atrasam.

## 1. Confirmar
1. `GET /health` da API: veja `entries.outbox` e `entries.postgres`.
2. No Seq, filtre `event.name = 'OutboxProcessingFailed'` e abra a exceção do evento mais recente: a
   mensagem diz se foi uma mensagem ("Falha ao processar ... (tentativa N)") ou o job inteiro.
3. Veja os spans `ProcessOutbox <Evento>` (fonte `Subsolo.Messaging`) com status de erro: a tag
   `subsolo.message.id` identifica a mensagem.
4. Avisos (SPEC-0031/0032): filtre `event.name = 'NoticeDeliveriesRunFailed' or event.name = 'DailyDigestRunFailed' or
   event.name = 'NoticesRetentionRunFailed' or event.name = 'DayBeforeReminderRunFailed'` e abra a exceção. Para as entregas de avisos em si (não o job inteiro), veja `docs/runbooks/avisos-sem-envio.md`.

## 2. Diagnosticar
- **PostgreSQL fora do ar** (`entries.postgres` Unhealthy, exceções do Npgsql): é a causa mais comum; trate o banco
  primeiro (`docs/runbooks/erros-5xx.md`, item 3). O job volta sozinho quando o banco volta.
- **Job não executa e não falha** (sem `OutboxProcessingFailed`, mas `entries.outbox` Unhealthy): o scheduler do
  Quartz parou ou está travado numa execução longa — veja os logs da categoria `Quartz` e reinicie a API.
- **Uma mensagem falhando sempre** (erro por mensagem, com novas tentativas): veja as colunas `error` e `attempts` da tabela
  `outbox_messages` do schema do módulo; o handler que falha está no log "Falha ao processar ...".

### Job agendado falhou: resumo diário
1. No Seq, filtre `event.name = 'DailyDigestRunFailed'` e abra a exceção: banco fora do ar (`entries.postgres` do
   `/health`) é a causa mais comum; erro do Notifications ou de configuração (`Engagement:DigestCron` inválido impede a
   API de subir) aparece na pilha.
2. Veja se o resumo do dia saiu para parte dos fãs: `select count(*) from notifications.deliveries where dedupe_key =
   'digest:<aaaa-mm-dd>'`.
3. Veja se o resumo do dia foi concluído (SPEC-0032): `select * from engagement.digest_runs order by digest_date desc
   limit 7`. Sem a linha do dia depois das 9h de São Paulo, o gatilho de recuperação ainda vai rodar o resumo.

## 3. Agir
- Reiniciar a API (`docker compose restart api`) depois de corrigir a causa; o job retoma as mensagens pendentes.
- Resumo diário que falhou (`DailyDigestRunFailed`) ou API fora às 9h: a **recuperação** é automática (SPEC-0032). O
  gatilho `Engagement:DigestCatchUpCron` (padrão `0 0/15 * * * ?`, a cada 15 min) olha `engagement.digest_runs`: depois
  das 9h de São Paulo, se não há linha do dia, roda o resumo com a janela (9h de ontem, 9h de hoje] e grava a linha ao
  concluir; antes das 9h, ou com a linha gravada, não faz nada. Corrigida a causa, basta esperar a próxima
  recuperação. Os shows publicados ficam 7 dias em `engagement.published_shows`; rodar de novo no mesmo dia não
  duplica e-mail (chave `digest:{data}` por fã). Para forçar um resumo de novo no mesmo dia, apague a linha do dia em
  `engagement.digest_runs` (a chave por fã continua evitando e-mail repetido).
- Lembrete na véspera que falhou (`DayBeforeReminderRunFailed`): corrigida a causa, a execução da hora seguinte envia
  os lembretes que faltam (a janela é a véspera inteira depois de `Engagement:ReminderHour`, 10h no fuso da cidade).
- Retenção que falhou (`NoticesRetentionRunFailed`): corrigida a causa, nada a fazer; a limpeza do dia seguinte apaga
  tudo o que passou de 90 dias.
- Bug num handler: corrija com uma spec `fix` (teste de regressão primeiro); as mensagens com falha são
  reprocessadas na próxima tentativa (backoff), sem duplicar handlers já executados.

## 4. Encerrar
`/health` volta a 200 com `entries.outbox` Healthy e os eventos `OutboxProcessingFailed` (e `NoticeDeliveriesRunFailed`,
`DailyDigestRunFailed`, `NoticesRetentionRunFailed`, `DayBeforeReminderRunFailed`) param. Registre a causa.
