---
id: ADR-0012
title: Custo de operação
status: accepted
origin: user
date: 2026-10-13
pillars: [custo]
baseline: custo@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: revisão no G4 (nenhuma dependência paga sem ADR) + estimativa revisada e aprovada no épico de go-live
---

# ADR-0012 — Custo de operação

## Contexto e Problema
Custo descoberto só na fatura inviabiliza produtos que funcionam, e escolhas de stack parecem baratas até a escala mudar. O Subsolo é gratuito para todos e se sustenta com apoio via Pix e patrocínio da cena, então o custo de operação precisa ser mínimo. O Thomas definiu: **tudo que gera custo é decidido só no final, quando a aplicação estiver redonda localmente.**

## Direcionadores da Decisão
- Escolha do usuário (origin: user): nenhum gasto até o go-live; as decisões pagas são tomadas no épico de go-live.
- VPS própria, já existente (o custo dela não é novo, mas o uso também é decidido no go-live).
- Receita incerta (Pix e patrocínio), então o custo variável precisa ser previsível.

## Opções Consideradas
- Desenvolvimento 100% local e gratuito; estimativa agora e decisões pagas no go-live
- Contratar os serviços já na fundação (deploy contínuo em produção desde o primeiro dia)

## Resultado da Decisão
**Opção escolhida:** desenvolvimento 100% local e gratuito até o go-live, porque é a escolha do Thomas. A fundação usa só o que não tem custo: Aspire local, containers locais (PostgreSQL, Seq, Mailpit para e-mail), GitHub com Actions gratuito e Web Push (gratuito por natureza). Esta estimativa serve de base para a decisão no go-live.

**Itens com custo, decididos no épico de go-live:**
| Item | Recomendação atual | Custo estimado | Verificação |
|---|---|---|---|
| Domínio `agendasubsolo.com.br` | Registro.br | cerca de R$ 40/ano | preço não verificado; disponível em 2026-10-13 |
| VPS | a própria do Thomas | já paga (sem custo novo) | detalhes a levantar no go-live |
| E-mail transacional | Amazon SES | US$ 0,10 por 1.000 e-mails | verificado em 2026-10-13 |
| Backup externo | Cloudflare R2 | US$ 0 até 10 GB | verificado em 2026-10-13 |
| Monitor de disponibilidade | plano gratuito (UptimeRobot ou similar) | US$ 0 | não verificado |
| GitHub | repositório público (Free) | US$ 0 | se for privado com proteção de branch: GitHub Pro (preço não verificado) |

**Premissas de uso e cenários (custo variável mensal):**
| Cenário | Premissas | E-mails/mês | R2 | Total variável estimado |
|---|---|---|---|---|
| Atual (meta de 6 meses) | 1 cidade, 1.000 fãs, ~20 shows/mês, metade dos avisos por e-mail em resumo diário | ~12 mil | < 1 GB | ~US$ 1,20 |
| 2x | 2 cidades, 2.000 fãs | ~24 mil | < 2 GB | ~US$ 2,40 |
| 10x | 10 cidades, 10.000 fãs | ~120 mil | ~5 GB | ~US$ 12 (+ provável upgrade de VPS) |

**Regras (verificáveis):**
- Estimativa mensal antes de escolher hospedagem e serviços, com premissas de uso explícitas e cenários (atual, 2x, 10x) — tabela acima.
- Preços verificados na fonte oficial com a data da verificação; o que não foi verificado está declarado.
- Nenhum serviço pago é contratado antes do épico de go-live; a estimativa é revista e aprovada pelo Thomas lá.
- Alerta de gasto configurado em US$ 5/mês na AWS (se o SES for escolhido) e revisão de custo real a cada 3 meses após o go-live.
- Recursos identificados por ambiente (`staging`/`production`) para rastrear o gasto.

### Consequências
- **Boa**, porque o produto é validado localmente sem gastar nada, e a decisão de custo é tomada com o produto pronto e a estimativa em mãos.
- **Ruim**, porque a primeira ida a produção (VPS, DNS, e-mail real, push com HTTPS) acontece mais tarde; problemas de ambiente aparecem no go-live, e não no primeiro dia. A mitigação é rodar localmente a mesma imagem e o mesmo Compose gerado pelo Aspire.

### Confirmação (G3)
Revisão no G4: nenhuma dependência ou serviço pago entra sem ADR; a estimativa deste ADR é revista e aprovada no H1 do épico de go-live.

## Prós e Contras das Opções
| Critério (peso) | Local primeiro, pago no go-live | Produção desde o início |
|---|---|---|
| Gasto antes de validar (3) | zero | pequeno, mas contínuo |
| Problemas de ambiente descobertos cedo (2) | não | sim |
| Escolha do usuário (3) | sim | não |

## Mais Informações
- Amazon SES: US$ 0,10 por 1.000 e-mails à la carte — https://aws.amazon.com/ses/pricing/ (verificado em 2026-10-13).
- Resend: 3.000/mês e 100/dia grátis; Pro a US$ 20/mês com 50 mil — https://resend.com/pricing (verificado em 2026-10-13).
- Cloudflare R2 — https://developers.cloudflare.com/r2/pricing/ (verificado em 2026-10-13).
- Registro.br: disponibilidade de `agendasubsolo.com.br` consultada em 2026-10-13; preço não verificado.
- Referência neutra: FinOps Foundation.
