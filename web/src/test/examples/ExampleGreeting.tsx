import type { JSX } from "react";

/** SPEC-0005 UT-03 — componente de exemplo do harness do front: mostra "Bem-vindo ao {name}" num título. */
export function ExampleGreeting({ name }: { name: string }): JSX.Element {
  throw new Error(
    `ExampleGreeting ainda não implementado (SPEC-0005): ${name}`,
  );
}
