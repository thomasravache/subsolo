import type { SVGProps } from "react";

/** SPEC-0027 — ícones de seguir, no traço dos ícones do shell; decorativos. */
const base = {
  viewBox: "0 0 24 24",
  width: 20,
  height: 20,
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 2,
  strokeLinecap: "square",
  "aria-hidden": true,
  focusable: false,
} as const;

/** Visto do botão "Seguindo". */
export function CheckIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...base} {...props}>
      <path d="M5 12l5 5L19 7" />
    </svg>
  );
}

/** Marcador da aba "Seguindo" (tela "Avisos e Seguindo" do Stitch). */
export function BookmarkIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...base} width={24} height={24} {...props}>
      <path d="M6 3h12v18l-6-4-6 4z" />
    </svg>
  );
}

/** SPEC-0029 — chama do "Vou" e da contagem "{n} vão" (tela "Página do Show" do Stitch). */
export function FlameIcon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg {...base} {...props}>
      <path d="M12 3c1 3.5 5 5.5 5 10a5 5 0 0 1-10 0c0-2.5 1.5-4 2.5-5 .3 1.6 1 2.6 2 3 0-3 .5-5.5.5-8z" />
    </svg>
  );
}
