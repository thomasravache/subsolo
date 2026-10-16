import type { ButtonHTMLAttributes } from "react";

export type ButtonVariant = "primary" | "secondary" | "ghost";
export type ButtonSize = "md" | "lg";

const base =
  "inline-flex min-h-11 min-w-11 items-center justify-center gap-2 rounded-md font-label text-sm font-bold uppercase tracking-wider transition-colors disabled:cursor-not-allowed disabled:opacity-50";

const variants: Record<ButtonVariant, string> = {
  primary: "bg-accent text-text-on-accent hover:brightness-110",
  secondary:
    "border border-border bg-surface text-text hover:border-text-muted",
  ghost: "text-text hover:bg-surface",
};

const sizes: Record<ButtonSize, string> = {
  md: "px-4 py-2",
  lg: "min-h-12 px-6 py-3 text-base",
};

/** Classes do botão, para aplicar o mesmo visual a um link (`<Link className={buttonClasses(...)}>`). */
export function buttonClasses(
  variant: ButtonVariant = "primary",
  size: ButtonSize = "md",
  className = "",
): string {
  return `${base} ${variants[variant]} ${sizes[size]} ${className}`.trim();
}

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
}

/** SPEC-0011 — botão do design system: alvo de toque ≥ 44 px e foco visível (anel global de `:focus-visible`). */
export function Button({
  variant = "primary",
  size = "md",
  className,
  type = "button",
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      className={buttonClasses(variant, size, className)}
      {...props}
    />
  );
}
