import Link from "next/link";

import { InstagramIcon } from "./icons";

export interface FooterLinks {
  apoie: string;
  instagram: string;
  privacidade: string;
}

/** Endereços padrão do rodapé (o Instagram do Subsolo é o único externo). */
export const defaultFooterLinks: FooterLinks = {
  apoie: "/apoie",
  instagram: "https://www.instagram.com/subsolo.app",
  privacidade: "/privacidade",
};

const footerLink =
  "inline-flex min-h-11 min-w-11 items-center gap-2 font-label text-xs font-bold uppercase tracking-wider";
const quietLink = `${footerLink} text-text-muted hover:text-text`;

export interface FooterProps {
  links?: FooterLinks;
}

/** SPEC-0011 — rodapé: "Apoie" (destaque), Instagram do Subsolo e privacidade. */
export function Footer({ links = defaultFooterLinks }: FooterProps) {
  return (
    <footer className="border-t border-border bg-bg">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-4 px-4 py-6 md:flex-row md:items-center md:justify-between">
        <p className="font-display text-lg uppercase tracking-widest text-text">
          Subsolo
          <span className="ml-3 font-label text-xs normal-case tracking-normal text-text-muted">
            A agenda do metal underground.
          </span>
        </p>
        <ul className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <li>
            <Link
              href={links.apoie}
              className={`${footerLink} text-text underline decoration-accent decoration-2 underline-offset-4`}
            >
              Apoie
            </Link>
          </li>
          <li>
            <a
              href={links.instagram}
              target="_blank"
              rel="noopener noreferrer"
              className={quietLink}
            >
              <InstagramIcon width={18} height={18} />
              Instagram
              <span className="sr-only"> (abre em nova aba)</span>
            </a>
          </li>
          <li>
            <Link href={links.privacidade} className={quietLink}>
              Privacidade
            </Link>
          </li>
        </ul>
      </div>
    </footer>
  );
}
