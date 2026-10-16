import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

// SPEC-0005 — sem `globals` no Vitest, o Testing Library não desmonta sozinho: limpa o DOM após cada teste.
afterEach(() => {
  cleanup();
});
