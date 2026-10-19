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
