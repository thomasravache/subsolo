// @vitest-environment jsdom
import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { CitySwitcher } from "@/components/ui/CitySwitcher";

/**
 * SPEC-0011 UT-04 — o `CitySwitcher` do cabeçalho é um seletor nativo (combobox com rótulo "Cidade", uma opção por
 * cidade com o slug como valor): escolher outra cidade chama `onCityChange` com o slug escolhido.
 */

const cities = [
  { slug: "cidade-exemplo", name: "Cidade Exemplo" },
  { slug: "curitiba", name: "Curitiba" },
];

describe("CitySwitcher", () => {
  it("SPEC-0011:UT-04 mostra a cidade atual entre as opções", () => {
    render(
      <CitySwitcher
        city="cidade-exemplo"
        cities={cities}
        onCityChange={() => {}}
      />,
    );

    const select = screen.getByRole<HTMLSelectElement>("combobox", {
      name: /cidade/i,
    });
    expect(select.value).toBe("cidade-exemplo");
    expect(
      screen.getAllByRole("option").map((option) => option.textContent),
    ).toEqual(["Cidade Exemplo", "Curitiba"]);
  });

  it("SPEC-0011:UT-04 escolher a outra cidade chama onCityChange com o slug", () => {
    const onCityChange = vi.fn();
    render(
      <CitySwitcher
        city="cidade-exemplo"
        cities={cities}
        onCityChange={onCityChange}
      />,
    );

    fireEvent.change(screen.getByRole("combobox", { name: /cidade/i }), {
      target: { value: "curitiba" },
    });

    expect(onCityChange).toHaveBeenCalledTimes(1);
    expect(onCityChange).toHaveBeenCalledWith("curitiba");
  });
});
