// @vitest-environment jsdom
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ExampleGreeting } from "./ExampleGreeting";

describe("ExampleGreeting", () => {
  it("SPEC-0005:UT-03 renderiza a saudação com o nome recebido", () => {
    render(<ExampleGreeting name="Subsolo" />);

    const heading = screen.getByRole("heading", {
      name: "Bem-vindo ao Subsolo",
    });
    expect(heading.textContent).toBe("Bem-vindo ao Subsolo");
  });
});
