# Runbook — Erros 5xx

Alerta do Seq **"Erros 5xx"** (`infra/seq/alerts.json`, SPEC-0009): mais de 5 respostas com status ≥ 500 (spans de
servidor da `api` ou do `web`) em 5 minutos.

## 1. Confirmar
1. Abra o Seq (local: dashboard do Aspire → `seq`; Compose: só por túnel, sem porta publicada) e rode o sinal
   **Erros 5xx**: `@SpanKind = 'Server' and coalesce(http.response.status_code, http.status_code) >= 500`.
2. Agrupe por rota e serviço: `select count(*) from stream where <filtro> group by @Resource.service.name, http.route`.
3. Abra um dos traces (`@TraceId`) para ver onde o erro nasceu: web → api → PostgreSQL.

## 2. Diagnosticar
- **`/health` da API** (`GET /health`): `entries.postgres` Unhealthy → banco fora do ar ou travado (vá ao item 3).
  `entries.outbox` Unhealthy → veja `docs/runbooks/outbox-parada.md`.
- **Exceções**: filtre `@Level = 'Error' and @Resource.service.name = 'api'` no mesmo intervalo; a mensagem e o tipo
  da exceção apontam a causa. Logs e traces já saem mascarados (ADR-0008): não procure e-mail ou token em texto puro.
- **Deploy recente?** Se os erros começaram logo após um deploy, siga o rollback do Plano de Rollout da spec
  (limite padrão: 5xx > 2% por 10 minutos).

## 3. Agir
- Banco parado: `docker compose ps postgres` / `docker compose logs postgres`; reinicie o serviço e confirme
  `/health` 200.
- Bug no código: reverta o merge commit do deploy (rollback) e abra uma spec `fix` com o teste de regressão.
- Dependência externa: registre o impedimento e acompanhe; a agenda mostra "indisponível" sem quebrar a página.

## 4. Encerrar
O alerta para sozinho quando a janela de 5 minutos fica abaixo do limite. Registre a causa e a ação tomada.
