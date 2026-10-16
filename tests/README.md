# Testes do Subsolo

Harness de testes de todos os níveis do [ADR-0005](../docs/adr/ADR-0005-estrategia-de-testes.md), montado pela
[SPEC-0005](../docs/specs/SPEC-0005-harness-de-testes.md). Cada nível tem um exemplo funcional para copiar.

## Comandos

Todos rodam na raiz do repositório (são os comandos do `docs/specs/sdd-config.yml`):

| Chave | Comando | O que roda |
|---|---|---|
| `test` | `dotnet test Subsolo.slnx --filter "Category!=Architecture&Kind!=GitHubVerification&Kind!=CommandContract" && (cd web && pnpm test)` | suíte .NET (unitários e integração) + Vitest do front |
| `test_integration` | `dotnet test Subsolo.slnx --filter "Kind=Integration"` | só os testes `Kind=Integration` |
| `test_commands_contract` | `dotnet test Subsolo.slnx --filter "Kind=CommandContract"` | sob demanda (G2/G5 locais): testes que rodam os próprios comandos do sdd-config; no CI cada comando já é um job |
| `arch_test` | `dotnet test tests/Subsolo.ArchitectureTests` | regras de arquitetura (ArchUnitNET) |
| `test_e2e` | `(cd e2e && pnpm test)` | Playwright nos projetos mobile e desktop |
| `coverage` | `./tools/coverage.sh` | cobertura (coverlet + ReportGenerator) e % das linhas alteradas vs `origin/main` |

Um projeto sem nenhum teste no filtro termina com o código 8 do Microsoft.Testing.Platform ("zero testes"). Cada
projeto de teste ignora esse código com
`<TestingPlatformCommandLineArguments>$(TestingPlatformCommandLineArguments) --ignore-exit-code 8</TestingPlatformCommandLineArguments>`
no `.csproj` — copie a linha ao criar um projeto de teste novo.

Um projeto só: `dotnet test --project tests/<Projeto>` (aceita `--filter`, ex.: `--filter "Spec=SPEC-0005:IT-01"`).

### Pré-requisitos

- **Docker** em execução para os testes de integração (Testcontainers). Sem ele, os fixtures falham logo com
  "Docker não encontrado: inicie o Docker para rodar testes de integração". O Testcontainers respeita `DOCKER_HOST`.
- Dependências do front e do E2E instaladas: `(cd web && pnpm install --frozen-lockfile)` e
  `(cd e2e && pnpm install --frozen-lockfile)`.
- Navegadores do Playwright, uma vez por máquina: `(cd e2e && pnpm exec playwright install --with-deps chromium webkit)`.
- Python 3 para o cálculo das linhas alteradas no `tools/coverage.sh`.

## Rastreabilidade

Todo teste carrega o ID do plano de testes da spec (`SPEC-NNNN:<ID>`, ex.: `SPEC-0008:UT-03`):

```csharp
[Fact]
[Trait("Spec", "SPEC-0008:UT-03")]
public void ...
```

```ts
test("SPEC-0008:E2E-01 fã vê a agenda da cidade no celular", async ({ page }) => { ... });
it("SPEC-0008:UT-05 mostra o nome da casa", () => { ... });
```

### Traits de tipo (`Kind`)

| Trait | Uso |
|---|---|
| (nenhum) | unitário: sem I/O, roda no `test` |
| `[Trait("Kind", "Integration")]` | usa Docker, processos externos ou rede local; entra no `test` e no `test_integration` |
| `[Trait("Kind", "CommandContract")]` | executa os próprios comandos do sdd-config (CT-01 da SPEC-0004 e da SPEC-0005); fora do `test`, do `test_integration` e do `coverage`, roda só com `test_commands_contract` |
| `[Trait("Kind", "GitHubVerification")]` | verificações reais no GitHub (SPEC-0006); fora do `test`, rodam sob demanda com `SDD_GITHUB_VERIFY=1` |
| `[Trait("Category", "Architecture")]` | testes de arquitetura que só devem rodar no `arch_test` |

## .NET

| Projeto | Conteúdo |
|---|---|
| `Subsolo.UnitTests.Common` | base dos unitários: Shouldly e `FakeTimeProvider` (`Microsoft.Extensions.Time.Testing`) |
| `Subsolo.IntegrationTests.Common` | `PostgresFixture`, `PostgresCollection`, `MailpitFixture` |
| `Subsolo.ArchitectureTests` | ArchUnitNET e o helper `ArchitectureRules` |
| `Examples/*` | exemplos do harness (modelo a copiar) e dublês dos testes da SPEC-0005 |

Os testes de cada módulo ficam em `tests/Modules/<M>/` e referenciam os projetos `*.Common`.

### Tempo

Código que depende de data/hora recebe um `TimeProvider`; o teste injeta um `FakeTimeProvider` e o avança
(`clock.Advance(TimeSpan.FromDays(1))`). Exemplo: `Examples/Subsolo.Examples.UnitTests/ExampleCalendarTests.cs`.

### PostgreSQL (`PostgresFixture`)

PostgreSQL 18 real (`postgres:18.6`) num container por coleção de testes. O projeto de teste inclui a definição da
coleção como link (o xUnit só encontra coleções no próprio assembly):

```xml
<Compile Include="../../Subsolo.IntegrationTests.Common/PostgresCollection.cs" Link="PostgresCollection.cs" />
```

```csharp
[Collection(nameof(PostgresCollection))]
public sealed class ShowRepositoryTests(PostgresFixture db)
{
    [Fact]
    [Trait("Spec", "SPEC-0008:IT-01")]
    [Trait("Kind", "Integration")]
    public async Task ...()
    {
        await db.ResetAsync("events");      // TRUNCATE … RESTART IDENTITY CASCADE nas tabelas do schema
        await using var connection = new NpgsqlConnection(db.ConnectionString);
        ...
    }
}
```

`ResetAsync()` sem schemas esvazia todas as tabelas dos schemas do usuário; o histórico de migrations do EF Core
(`__EFMigrationsHistory`) nunca é apagado. Os testes de uma coleção rodam em sequência; cada um chama `ResetAsync`
no início e não depende da ordem.

### E-mail (`MailpitFixture`)

Servidor SMTP de teste (`axllent/mailpit:v1.31.3`). Use como `IClassFixture<MailpitFixture>`:

```csharp
public sealed class AvisoTests(MailpitFixture mail) : IClassFixture<MailpitFixture>
{
    // configure o SMTP do código sob teste com mail.SmtpHost e mail.SmtpPort
    // await mail.ClearAsync();
    // var recebidas = await mail.GetMessagesAsync("fa@subsolo.test"); // Subject, From, To, Text
}
```

### Arquitetura (`ArchitectureRules`)

```csharp
ArchitectureRules.ForModule("Events")
    .DomainMustNotDependOn("Infrastructure", "Microsoft.EntityFrameworkCore")
    .OnlyReferences("Venues", "Bands");
```

- Camadas reconhecidas pelo namespace `….<Módulo>.<Camada>`; alvo sem ponto é camada do módulo, com ponto é
  namespace externo.
- Sem assemblies, avalia os `Subsolo.Modules.<Módulo>.*.dll` ao lado do assembly de teste (referencie os projetos do
  módulo); `ForModule("X", assembly1, assembly2)` avalia só os informados.
- Cada regra é avaliada na hora e falha o teste nomeando os tipos da dependência proibida.
- `OnlyReferences` permite, de outros módulos, só os namespaces `<Outro>.Contracts`.

## Front (Vitest)

Vitest 5 + Testing Library + jsdom (`web/vitest.config.ts`). Os testes ficam ao lado do código, em `*.test.ts(x)`
dentro de `web/src`. Exemplo: `web/src/test/examples/ExampleGreeting.test.tsx`.

```bash
(cd web && pnpm test)              # uma vez
(cd web && pnpm exec vitest)       # modo watch
```

## E2E (Playwright)

Projeto em `e2e/` (TypeScript), com os projetos `mobile-chrome` (Pixel 7), `mobile-safari` (iPhone 14) e
`desktop-chrome`. Importe `test` e `expect` do fixture local para ter o `axe`:

```ts
import { expect, test } from "./fixtures";

test("SPEC-0008:E2E-01 agenda acessível", async ({ page, axe }) => {
  await page.goto("/");
  await axe.check(page); // falha com violações serious/critical, nomeando as regras (ex.: color-contrast)
});
```

```bash
(cd e2e && pnpm test)                                   # todos os projetos
(cd e2e && pnpm exec playwright test --project mobile-chrome)
(cd e2e && pnpm exec playwright test --ui)              # modo interativo
(cd e2e && pnpm exec playwright show-report)            # relatório HTML (gerado no CI)
```

O `webServer` do `e2e/playwright.config.ts` serve `e2e/static` (páginas do harness, em `/harness/*.html`) com
`e2e/static-server.mjs`; as jornadas reais trocam o `baseURL`/`webServer` pela aplicação completa.

## Cobertura

`./tools/coverage.sh` compila a solution em Release, roda cada projeto de teste sob o coverlet
(`coverlet.console`, via `dotnet tool exec`) com o filtro do comando `test` e junta os resultados com o ReportGenerator
em `coverage/` (`index.html`, `Summary.txt`, `Cobertura.xml`). No fim, mostra a porcentagem das linhas alteradas em
relação a `origin/main` (merge-base), por arquivo e no total, e grava `coverage/changed-lines.md`:

```text
src/Subsolo.Modules.Events/Domain/Show.cs  9/10  90%
Total: 9/10 linhas alteradas cobertas — 90%
```

O script só reporta; o mínimo (`coverage_min_changed_lines` do sdd-config, 80%) é conferido no gate G2. Variáveis:
`COVERAGE_BASE` (padrão `origin/main`) e `COVERAGE_FILTER`.
