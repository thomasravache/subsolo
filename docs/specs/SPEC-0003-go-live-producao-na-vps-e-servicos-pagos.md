---
id: SPEC-0003
title: "Go-live: produção na VPS e serviços pagos"
tier: epic
type: foundation
status: proposed
created: 2026-10-13
depends_on: [SPEC-0001]
adrs: []
external: []
approved_by:
approved_at:
---

# SPEC-0003 — Go-live: produção na VPS e serviços pagos (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0003`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
Levar o Subsolo, já redondo localmente, para produção em `agendasubsolo.com.br`, com staging, deploy pelo pipeline, backup externo testado e monitoramento — **e é aqui que o Thomas decide tudo o que gera custo** (ADR-0012), com a estimativa revisada e o produto pronto em mãos.

## 2. Escopo
**Objetivos (dentro do escopo):**
- Decisão de custos: revisar e aprovar a estimativa do ADR-0012 (domínio, uso da VPS, e-mail, backup, monitor).
- Criar a organização gratuita `subsolo-br` no GitHub e transferir `thomasravache/subsolo` para ela (Emenda v1 da SPEC-0004); conferir redirecionamentos, GHCR e segredos.
- Registro do domínio `agendasubsolo.com.br` e do DNS (raiz + `staging`).
- Provisionamento da VPS como código (`infra/`): usuário de deploy, Docker, firewall, Caddy com TLS público, Seq de produção.
- Pipeline de deploy (`deploy.yml`): imagens, migrações por bundle EF, staging automático com smoke + E2E, produção com aprovação; rollback ensaiado.
- Provedor de e-mail transacional (recomendação: Amazon SES) com SPF, DKIM e DMARC, e a flag `avisos-email` ligada.
- Backup diário com restic para o R2 e teste de restauração mensal (ADR-0011).
- Monitor externo de disponibilidade e alertas de produção (ADR-0010).

- Endurecimento do artefato de deploy apontado na revisão da SPEC-0008: cabeçalhos de segurança no Caddy (HSTS, `X-Content-Type-Options`, `Referrer-Policy`, `frame-ancestors`), imagem do web sem root, imagens base com versão completa ou digest (Node e `aspnet`), Seq de produção com autenticação e sem Mailpit no artefato de produção, portas ligadas só ao necessário.

**Não-objetivos (fora do escopo):**
- Features novas do produto (épico SPEC-0002).
- Alta disponibilidade (múltiplas VPS) e CDN — avaliar depois do lançamento, com dados reais de uso.

## 3. Arquitetura Alvo
**Contexto:** projeto novo; ADR-0004 (Aspire + Compose + Caddy + GitHub Actions), ADR-0010, ADR-0011 e ADR-0012.

A VPS roda dois projetos Compose isolados (`subsolo-staging` e `subsolo-production`), cada um com API, web e PostgreSQL próprios, atrás de um Caddy compartilhado; o Seq de produção é acessível só por túnel SSH. O backup sai da VPS para o R2.

**Decisões (ADRs):** ADR-0004, ADR-0010, ADR-0011 e ADR-0012; a escolha final do provedor de e-mail atualiza o ADR-0014.

**Regras de arquitetura a garantir (G3):** nenhum deploy fora do pipeline; os segredos só nos environments do GitHub e no `.env` restrito (ADR-0008); staging sem dados de produção (ADR-0011).

## 4. Decomposição
As filhas são criadas quando a fundação fechar e o Thomas decidir os custos.

| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| a criar | Provisionamento da VPS como código e TLS público | full | foundation | M | fundação | — |
| a criar | Pipeline de deploy staging/produção com rollback | full | foundation | M | Provisionamento | — |
| a criar | E-mail transacional em produção (SPF/DKIM/DMARC) | full | foundation | S | Pipeline de deploy | — |
| a criar | Backup externo e teste de restauração | full | foundation | S | Provisionamento | — |
| a criar | Monitor externo e alertas de produção | full | foundation | S | Pipeline de deploy | — |

## 5. Estratégia de Entrega
- **Ambientes:** staging (`staging.agendasubsolo.com.br`) e produção (`agendasubsolo.com.br`) na VPS do Thomas.
- **Entrega por onda:** 1) VPS + TLS + deploy de staging; 2) produção + backup + monitor; 3) e-mail real + flag `avisos-email`.
- **Feature flags:** `avisos-email` (ver SPEC-0002).
- **Rollback:** deploy da tag anterior em um passo (ADR-0004), ensaiado em staging antes da primeira release de produção.
- **Métricas de sucesso pós-release:** SLOs do ADR-0010 cumpridos por 30 dias; restauração mensal OK; custo real dentro da estimativa aprovada.

## 6. Riscos & Mitigações
- A VPS existente já roda outras coisas — mitigação: levantar recursos e conflitos de porta antes de provisionar (questão abaixo).
- E-mails caindo no spam — mitigação: SPF, DKIM e DMARC e aquecimento gradual do volume.
- Staging e produção na mesma VPS — mitigação: projetos Compose, bancos e volumes separados e limites de memória por container.

## 7. Critérios de Aceite do Épico
Os IDs de teste são definidos quando as filhas forem criadas.
- [ ] Merge na `main` publica em staging e roda smoke + E2E automaticamente
- [ ] A release de produção passa pelo pipeline com aprovação humana, e o rollback foi ensaiado
- [ ] O backup diário é restaurado com sucesso no teste mensal
- [ ] Queda do site gera alerta externo em até 5 minutos

## 8. Questões em Aberto
- [ ] Provedor, região, vCPU/RAM/disco e sistema operacional da VPS, e o que mais já roda nela? (Thomas)
- [ ] Aprovação dos custos do ADR-0012 revisados: domínio, e-mail (recomendado: Amazon SES), backup (recomendado: Cloudflare R2), monitor externo (Thomas)
- [ ] Endereço definitivo do Instagram do Subsolo (perfil ainda não criado; hoje o rodapé aponta para o provisório `https://www.instagram.com/subsolo.app` em `web/src/components/ui/Footer.tsx`, SPEC-0011) — trocar antes do lançamento (Thomas)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0003` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
