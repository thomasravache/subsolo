---
id: ADR-0016
title: Status HTTP das páginas SSR via proxy do Next.js
status: accepted
origin: decision
date: 2026-10-16
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: E2E-01 (404 da cidade inexistente) e IT-04 (503 com a API fora) da SPEC-0008, rodando em todo PR
---

# ADR-0016 — Status HTTP das páginas SSR via proxy do Next.js

## Contexto e Problema
As páginas públicas (agenda da cidade, depois show e perfil) precisam responder com o status HTTP certo: 404 para recurso inexistente, que buscadores e prévias de link levam em conta, e 503 quando a API está fora. No Next.js 16.4 com Cache Components, o shell estático sai primeiro com 200, e `notFound()` chamado depois de um `await` não muda mais o status. Como a página então devolve 404 e 503 de verdade?

## Direcionadores da Decisão
- Status HTTP correto nas páginas públicas (SEO e prévias de link do ADR-0013).
- Manter Cache Components e SSR (ADR-0013).
- Regra de negócio só na API .NET; o front usa apenas o cliente tipado.

## Opções Consideradas
- `proxy.ts` (antigo middleware) consulta a API pelo cliente tipado e reescreve a requisição com status 404/503 e um cabeçalho de estado; a página exporta `unstable_paramMatching = { cidade: "blocking" }` para não sobrescrever o status
- Desligar Cache Components nas páginas públicas e usar `notFound()` direto
- Aceitar 200 com o conteúdo de "não encontrado" (soft 404)

## Resultado da Decisão
**Opção escolhida:** `proxy.ts` + `unstable_paramMatching` (escolhida pelo Architect na SPEC-0008 e aceita sob a autorização de piloto automático do Thomas em 2026-10-16), porque mantém Cache Components e entrega o status correto sem duplicar regra de negócio.

### Consequências
- **Boa**, porque 404 e 503 são reais, e a página continua SSR com cache.
- **Ruim**, porque a API é chamada duas vezes por visualização (proxy e página); `unstable_paramMatching` não é documentado e pode mudar numa atualização do Next; e o matcher do proxy (`/:cidade([a-z0-9-]{2,60})`) precisa de exclusões quando surgirem páginas estáticas com caminho parecido (ex.: `/sobre`). Mitigação: E2E-01 e IT-04 quebram se o comportamento mudar; revisar ao atualizar o Next e trocar pela API estável quando existir. Na prática são até 3 chamadas por visualização (proxy, `generateMetadata` e página; a deduplicação do `fetch` não é garantida com `AbortSignal` distintos), com pior caso de ~10 s se a API não responder (timeout de 5 s em duas chamadas). O proxy remove qualquer cabeçalho interno de estado vindo do cliente (evita envenenamento de cache).

### Confirmação (G3)
E2E-01 (cidade inexistente → 404) e IT-04 (API fora → 503) da SPEC-0008, em todo PR pelo job `e2e`.

## Prós e Contras das Opções
| Critério (peso) | proxy + paramMatching | sem Cache Components | soft 404 |
|---|---|---|---|
| Status correto (3) | sim | sim | não |
| Mantém cache/SSR do ADR-0013 (2) | sim | não | sim |
| API estável do Next (2) | não (unstable) | sim | sim |
| Custo por requisição (1) | 2 chamadas à API | 1 | 1 |

## Mais Informações
- Publicação do front no Compose usa `PublishAsPackageScript` (ASPIREJAVASCRIPT001), API em avaliação no Aspire 13.6 — alternativa se mudar: `output: "standalone"` com Dockerfile próprio.
- `openapi-typescript` 7.13.0 declara peer `typescript ^5.x`; o projeto usa TypeScript 6.0.3 (ADR-0006) e a geração funciona (CT-01 verde); atualizar quando houver versão compatível.
- Next.js 16.4 (Cache Components, `proxy.ts`) — comportamento verificado no código e nos testes da SPEC-0008 em 2026-10-16; `unstable_paramMatching` não tem documentação pública (não verificado em fonte oficial).
