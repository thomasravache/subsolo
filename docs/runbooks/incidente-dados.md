# Runbook — Incidente com dados pessoais

Incidente de segurança é qualquer acesso, perda, alteração ou vazamento não autorizado de dado pessoal guardado pelo
Subsolo (tabela do ADR-0008: e-mail, nome, ID Google, inscrições de push, o que o fã segue, contato dos perfis, IP e
navegador nos logs). Exemplos: banco ou backup exposto, credencial vazada (segredo no log ou no repositório), acesso
indevido à VPS, bug que mostra dados de uma conta para outra.

A LGPD (art. 48) e o Regulamento de Comunicação de Incidente de Segurança da ANPD (Resolução CD/ANPD nº 15/2024)
pedem comunicar à ANPD e aos titulares, em até **3 dias úteis** da ciência, o incidente que possa causar risco ou dano
relevante. A política de privacidade (`/privacidade`) promete esse aviso.

## 1. Conter (na primeira hora)
1. Feche a porta: revogue ou troque o que vazou (segredos nos environments do GitHub e no `.env` da VPS, ADR-0008;
   rotação imediata), tire do ar o que está expondo dados (endpoint, porta, bucket) e bloqueie o acesso indevido
   (chave SSH, conta admin — retire o e-mail de `Users:AdminEmails`).
2. Bug na aplicação: reverta o merge commit que o introduziu (rollback do Plano de Rollout da spec) ou suba a correção
   com uma spec `fix`.
3. Preserve as evidências antes de limpar: logs e traces do Seq (exporte o período), logs do Caddy, histórico do
   GitHub Actions. Não apague registros.

## 2. Avaliar
- **O quê**: quais dados (categorias da tabela do ADR-0008), de quantos titulares, desde quando.
- **Como**: causa e alcance (quem pode ter acessado; o dado saiu ou só ficou exposto).
- **Risco**: há risco ou dano relevante? Pese o tipo de dado (e-mail e nome são não sensíveis; o que o fã segue pode
  revelar preferências), o volume, se os dados estavam protegidos (backups criptografados, tokens só como hash) e se
  dá para identificar as pessoas.
- Anote a data e a hora em que o incidente foi conhecido: o prazo de 3 dias úteis conta daí.

## 3. Comunicar (com risco relevante, em até 3 dias úteis)
- **ANPD**: formulário de comunicação de incidente no site da ANPD (gov.br/anpd), com a descrição, os dados e os
  titulares afetados, as medidas de contenção e de mitigação e, se passou do prazo, o motivo.
- **Titulares**: e-mail a cada pessoa afetada, em linguagem simples: o que aconteceu, quais dados, o risco, o que o
  Subsolo já fez e o que a pessoa pode fazer (ex.: atenção a e-mails falsos), com o canal de contato
  (`Site:ContactEmail`). Sem dado do titular no corpo além do necessário.
- Sem risco relevante: não é preciso comunicar, mas registre a avaliação (item 4).

## 4. Registrar
Registre todo incidente, mesmo sem comunicação (a ANPD pode pedir o registro por 5 anos): data e hora do incidente e
da ciência, dados e titulares afetados, avaliação de risco, medidas tomadas, comunicações feitas (com as datas) e a
causa. Guarde fora do repositório, junto aos documentos do Subsolo. A causa vira spec (`fix` ou melhoria de
segurança) com teste de regressão.

## 5. Encerrar
A causa foi corrigida e verificada, os segredos afetados foram trocados, as comunicações devidas foram feitas e o
registro está completo. Reveja o `docs/runbooks/checklist-seguranca.md` à luz do que aconteceu.
