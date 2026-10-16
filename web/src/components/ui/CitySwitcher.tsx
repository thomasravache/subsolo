/** Cidade oferecida no seletor: `slug` da URL (`/{slug}`) e nome exibido. */
export interface CityOption {
  slug: string;
  name: string;
}

/**
 * Seletor de cidade do cabeçalho: mostra a cidade atual (`city`, slug) entre `cities` e chama `onCityChange` com o
 * slug escolhido.
 */
export interface CitySwitcherProps {
  city: string;
  cities: CityOption[];
  onCityChange: (slug: string) => void;
}

// Scaffold: sem implementação.
export function CitySwitcher(props: CitySwitcherProps) {
  void props;
  return null;
}
