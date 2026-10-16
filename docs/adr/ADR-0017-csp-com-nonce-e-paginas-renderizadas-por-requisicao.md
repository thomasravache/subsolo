---
id: ADR-0017
title: CSP com nonce e páginas renderizadas por requisição
status: accepted
origin: decision
date: 2026-10-16
pillars: [seguranca-acesso]
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: UT-02, IT-05 e E2E-01 da SPEC-0010 (CSP com nonce presente e nenhuma violação de CSP no navegador em `/`, na agenda e na página de não encontrado)
---

# ADR-0017 — CSP com nonce e páginas renderizadas por requisição

## Contexto e Problema
A SPEC-0010 exige uma Content-Security-Policy com nonce (`script-src 'self' 'nonce-…'`) em todas as páginas do web. No Next.js 16, o nonce só é aplicado aos scripts de páginas renderizadas por requisição; páginas pré-renderizadas (shell estático do Cache Components/PPR) saem sem nonce e teriam os próprios scripts bloqueados. Isso foi medido pelo Test-writer da SPEC-0010 em 2026-10-16: `/` estática com 15 scripts e nenhum nonce; `/cidade-exemplo`, renderizada por requisição, com 13 de 13. Como conciliar a CSP com nonce com o Cache Components do ADR-0013?

## Direcionadores da Decisão
- Segurança: CSP estrita contra XSS, sem `'unsafe-inline'` em scripts (ADR-0003, OWASP).
- Manter o Next.js 16 com Cache Components (ADR-0013) e o status HTTP real das páginas (ADR-0016).
- Custo de desempenho aceitável: VPS única, tráfego inicial pequeno, páginas leves.

## Opções Consideradas
- Páginas renderizadas por requisição (`await connection()` no layout público e nas páginas com HTML), mantendo `cacheComponents: true` e o cache de dados
- CSP por hash/SRI (experimental no Next 16) ou com `'unsafe-inline'`
- Desligar o Cache Components

## Resultado da Decisão
**Opção escolhida:** páginas renderizadas por requisição (recomendação do Test-writer e do Architect, aceita sob a autorização de piloto automático do Thomas em 2026-10-16), porque é o caminho documentado pelo Next.js para CSP com nonce e mantém a política estrita sem trocar de framework.

### Consequências
- **Boa**, porque a CSP com nonce vale em todas as páginas, e a agenda (já renderizada por requisição pelo ADR-0016) não muda.
- **Ruim**, porque `/` e a página de não encontrado perdem o shell estático (mais CPU por requisição e um TTFB um pouco maior); é um desvio do "cache curto" das páginas públicas do ADR-0013, mitigado pelo cache de dados das chamadas à API, que continua valendo. Revisar quando o Next estabilizar CSP por hash compatível com PPR.

### Confirmação (G3)
UT-02 (gerador de CSP com nonce novo por requisição), IT-05 (cabeçalhos pelo Caddy) e E2E-01 (nenhum evento `securitypolicyviolation` em `/`, na agenda e na página de não encontrado) da SPEC-0010.

## Prós e Contras das Opções
| Critério (peso) | Renderização por requisição | Hash/SRI ou `unsafe-inline` | Sem Cache Components |
|---|---|---|---|
| CSP estrita (3) | sim | experimental / não | sim |
| Mantém ADR-0013/0016 (2) | quase (sem shell estático em 2 páginas) | sim | não |
| Desempenho (1) | menor nas páginas estáticas | igual | menor em todas |

## Mais Informações
- Guia de CSP do Next.js 16 (`node_modules/next/dist/docs/01-app/02-guides/content-security-policy.md`, lido em 2026-10-16): nonce exige renderização dinâmica; "Partial Prerendering (PPR) is incompatible with nonce-based CSP".
