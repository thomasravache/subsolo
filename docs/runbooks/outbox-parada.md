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

Enquanto a outbox está parada, os eventos de domínio ficam gravados (nada se perde), mas os efeitos assíncronos
(avisos, integrações entre módulos) atrasam.

## 1. Confirmar
1. `GET /health` da API: veja `entries.outbox` e `entries.postgres`.
2. No Seq, filtre `event.name = 'OutboxProcessingFailed'` e abra a exceção do evento mais recente: a
   mensagem diz se foi uma mensagem ("Falha ao processar ... (tentativa N)") ou o job inteiro.
3. Veja os spans `ProcessOutbox <Evento>` (fonte `Subsolo.Messaging`) com status de erro: a tag
   `subsolo.message.id` identifica a mensagem.

## 2. Diagnosticar
- **PostgreSQL fora do ar** (`entries.postgres` Unhealthy, exceções do Npgsql): é a causa mais comum; trate o banco
  primeiro (`docs/runbooks/erros-5xx.md`, item 3). O job volta sozinho quando o banco volta.
- **Job não executa e não falha** (sem `OutboxProcessingFailed`, mas `entries.outbox` Unhealthy): o scheduler do
  Quartz parou ou está travado numa execução longa — veja os logs da categoria `Quartz` e reinicie a API.
- **Uma mensagem falhando sempre** (erro por mensagem, com novas tentativas): veja as colunas `error` e `attempts` da tabela
  `outbox_messages` do schema do módulo; o handler que falha está no log "Falha ao processar ...".

## 3. Agir
- Reiniciar a API (`docker compose restart api`) depois de corrigir a causa; o job retoma as mensagens pendentes.
- Bug num handler: corrija com uma spec `fix` (teste de regressão primeiro); as mensagens com falha são
  reprocessadas na próxima tentativa (backoff), sem duplicar handlers já executados.

## 4. Encerrar
`/health` volta a 200 com `entries.outbox` Healthy e os eventos `OutboxProcessingFailed` param. Registre a causa.
