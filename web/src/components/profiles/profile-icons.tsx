import type { SVGProps } from "react";

/**
 * SPEC-0026 — ícones de traço dos links das redes na página do perfil (24×24, `currentColor`, no desenho dos de
 * `components/ui/icons`), decorativos: o nome acessível do link dá a rede.
 */
function Icon({ children, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      width="20"
      height="20"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="square"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  );
}

/** Fones (Bandcamp, como no Stitch). */
export function HeadphonesIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <Icon {...props}>
      <path d="M4 18v-6a8 8 0 0 1 16 0v6M4 14h4v6H4zM16 14h4v6h-4z" />
    </Icon>
  );
}

/** Livro (Metal Archives, como no Stitch). */
export function BookIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <Icon {...props}>
      <path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3zM5 17a3 3 0 0 1 3-3h11" />
    </Icon>
  );
}

/** Pino de lugar (cidade do perfil). */
export function PlaceIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <Icon {...props}>
      <path d="M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z" />
      <circle cx="12" cy="10" r="2.5" />
    </Icon>
  );
}
