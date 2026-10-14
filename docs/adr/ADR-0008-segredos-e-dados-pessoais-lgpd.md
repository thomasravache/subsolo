---
id: ADR-0008
title: Segredos e dados pessoais (LGPD)
status: accepted
origin: decision
date: 2026-10-13
pillars: [segredos-dados]
baseline: segredos-dados@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: gitleaks no CI e no pre-commit (security_scan) + teste de mascaramento de logs
---

# ADR-0008 — Segredos e dados pessoais (LGPD)

## Contexto e Problema
Credencial no código ou no log e dado pessoal sem controle são as falhas de segurança e de conformidade mais comuns. O Subsolo guarda dados pessoais de fãs e artistas (e-mail, nome, conta Google, inscrições de push, quem segue o quê e quem vai a qual show) e precisa de segredos (Google OAuth, chave VAPID, credencial de e-mail, chave de backup, SSH de deploy). O produto opera no Brasil: aplica-se a LGPD.

## Direcionadores da Decisão
- LGPD: base legal, finalidade, retenção e direitos do titular (acesso e exclusão).
- Repositório possivelmente público (ADR-0007): nenhum segredo pode depender de o repositório ser privado.
- Uma VPS e uma pessoa operando: solução simples, sem cofre dedicado.

## Opções Consideradas
- Segredos de ambiente do GitHub Actions → arquivo `.env` com permissão restrita na VPS; `dotnet user-secrets` + parâmetros do Aspire no desenvolvimento
- Cofre dedicado (HashiCorp Vault/OpenBao ou Infisical self-hosted)
- Cofre gerenciado de nuvem (AWS Secrets Manager, Doppler)

## Resultado da Decisão
**Opção escolhida:** segredos nos *environments* `staging` e `production` do GitHub Actions, gravados pelo deploy num `.env` por ambiente na VPS (dono `deploy`, permissão 600, fora do diretório do repositório); no desenvolvimento, `dotnet user-secrets` e parâmetros `secret` do Aspire. Um cofre dedicado seria mais um serviço para proteger e manter, sem ganho real para uma pessoa e uma VPS.

**Dados pessoais e LGPD:**

| Dado | Finalidade | Base legal | Retenção |
|---|---|---|---|
| E-mail, nome, ID Google | conta e login | execução de contrato (termos de uso) | enquanto a conta existir |
| Inscrição de push, preferências de aviso | enviar avisos pedidos pelo fã | consentimento (opt-in explícito, revogável) | até revogar ou excluir a conta |
| Seguir, "vou" | agenda personalizada, contagem e lembretes | execução de contrato | enquanto a conta existir; a contagem pública é anônima |
| IP e user agent em logs | segurança e diagnóstico | legítimo interesse | 30 dias |
| Contato do produtor/banda | aprovação e moderação | execução de contrato | enquanto o perfil existir |

- Página de privacidade e termos de uso antes do lançamento; o fã pode baixar e **excluir** os próprios dados pela conta.
- A lista de quem marcou "vou" não é pública: só a contagem aparece.

**Regras (verificáveis):**
- Nenhum segredo no repositório; o gitleaks no CI bloqueia o merge e roda também no hook de pre-commit.
- Segredos ficam nos environments do GitHub e no `.env` restrito da VPS, separados por ambiente, com acesso mínimo (a chave SSH de deploy só pode rodar o script de deploy).
- Rotação de credenciais a cada 12 meses e imediata em caso de vazamento.
- Dados pessoais classificados na tabela acima, com base legal, finalidade e prazo de retenção.
- Dados pessoais e segredos nunca aparecem em logs: e-mail mascarado, token nunca logado (teste de mascaramento).
- Criptografia em trânsito (TLS pelo Caddy, HSTS) e em repouso nos backups (restic, ADR-0011); a criptografia do disco da VPS depende do provedor (ver Questões em Aberto do SPEC-0001).

### Consequências
- **Boa**, porque é simples, sem custo, e o repositório pode ser público com segurança.
- **Ruim**, porque quem tem acesso root à VPS lê os segredos; a mitigação é SSH só com chave, sem login root e com firewall (ADR-0004/infra).

### Confirmação (G3)
Varredura de segredos (gitleaks) em `security_scan`; teste de mascaramento de logs no plano de testes do walking skeleton; testes de exclusão de conta na spec de contas.

## Prós e Contras das Opções
| Critério (peso) | Environments GitHub + .env | Cofre self-hosted | Cofre gerenciado |
|---|---|---|---|
| Custo (3) | zero | zero (mas consome RAM) | pago acima do plano gratuito |
| Operação (3) | mínima | alta | baixa |
| Auditoria e rotação (2) | manual | completa | completa |

## Mais Informações
- Lei 13.709/2018 (LGPD) — https://www.planalto.gov.br/ccivil_03/_ato2015-2018/2018/lei/l13709.htm.
- GitHub Actions: segredos de environment — https://docs.github.com/actions/security-for-github-actions/security-guides/using-secrets-in-github-actions (não reverificado nesta data).
- Referências neutras: OWASP ASVS (V6, V8).
