import Form from "next/form";

import type { Style } from "@/api/admin";
import { Field, fieldControl } from "@/components/admin/Field";
import { Button } from "@/components/ui/Button";
import { ChipLink } from "@/components/ui/Chip";
import { CalendarIcon } from "@/components/ui/icons";

import {
  agendaHref,
  type AgendaFilters,
  type AgendaPeriodFilter,
} from "./agenda-filters";

export interface AgendaFilterBarProps {
  citySlug: string;
  filters: AgendaFilters;
  styles: Style[];
  /** `periodo=escolher`: o formulário de datas aberto. */
  choosing: boolean;
}

/**
 * SPEC-0025 — filtros da agenda (Stitch "Agenda Curitiba"): chips de período ("ESTA SEMANA", "ESTE MÊS", "ESCOLHER
 * DATA") e de estilo, todos links (`aria-current="page"` no selecionado) que trocam a URL e mantêm o outro filtro; tocar
 * no chip selecionado tira o filtro. "Escolher data" abre um formulário GET com "De", "Até" e "Aplicar" (Enter também
 * envia), que leva os estilos escolhidos junto. No celular, cada linha rola na horizontal; no desktop, coluna lateral.
 */
export function AgendaFilterBar({
  citySlug,
  filters,
  styles,
  choosing,
}: AgendaFilterBarProps) {
  const range = filters.period?.kind === "intervalo" ? filters.period : null;
  const periodHref = (period: AgendaPeriodFilter) =>
    agendaHref(citySlug, {
      ...filters,
      period: filters.period?.kind === period.kind ? undefined : period,
    });
  const styleHref = (slug: string) =>
    agendaHref(citySlug, {
      ...filters,
      styles: filters.styles.includes(slug)
        ? filters.styles.filter((style) => style !== slug)
        : [...filters.styles, slug],
    });
  const dateOpen = choosing || range !== null;

  return (
    <div className="flex flex-col gap-3">
      <nav aria-label="Período" className="flex flex-col gap-2">
        <h2 className="hidden font-label text-xs font-bold uppercase tracking-wider text-text-muted lg:block">
          Datas
        </h2>
        <ul className="-mx-4 flex gap-2 overflow-x-auto px-4 py-0.5 lg:mx-0 lg:flex-col lg:items-stretch lg:px-0">
          <li>
            <ChipLink
              href={periodHref({ kind: "semana" })}
              selected={filters.period?.kind === "semana"}
            >
              Esta semana
            </ChipLink>
          </li>
          <li>
            <ChipLink
              href={periodHref({ kind: "mes" })}
              selected={filters.period?.kind === "mes"}
            >
              Este mês
            </ChipLink>
          </li>
          <li>
            <ChipLink
              href={agendaHref(
                citySlug,
                { ...filters, period: undefined },
                !dateOpen,
              )}
              selected={dateOpen}
            >
              <CalendarIcon width={14} height={14} />
              Escolher data
            </ChipLink>
          </li>
        </ul>
      </nav>

      {dateOpen && (
        <Form
          action={`/${encodeURIComponent(citySlug)}`}
          scroll={false}
          aria-label="Escolher data"
          className="flex flex-wrap items-end gap-3 rounded-md border border-border bg-surface p-3 lg:flex-col lg:items-stretch"
        >
          <Field id="agenda-de" label="De">
            <input
              id="agenda-de"
              name="de"
              type="date"
              required
              defaultValue={range?.from}
              className={fieldControl}
            />
          </Field>
          <Field id="agenda-ate" label="Até">
            <input
              id="agenda-ate"
              name="ate"
              type="date"
              required
              defaultValue={range?.to}
              className={fieldControl}
            />
          </Field>
          {filters.styles.length > 0 && (
            <input
              type="hidden"
              name="estilo"
              value={filters.styles.join(",")}
            />
          )}
          <Button type="submit">Aplicar</Button>
        </Form>
      )}

      {styles.length > 0 && (
        <nav aria-label="Estilos" className="flex flex-col gap-2">
          <h2 className="hidden font-label text-xs font-bold uppercase tracking-wider text-text-muted lg:block">
            Estilos
          </h2>
          <ul className="-mx-4 flex gap-1.5 overflow-x-auto px-4 py-0.5 lg:mx-0 lg:flex-wrap lg:px-0">
            {styles.map((style) => (
              <li key={style.slug}>
                <ChipLink
                  href={styleHref(style.slug)}
                  selected={filters.styles.includes(style.slug)}
                >
                  {style.name}
                </ChipLink>
              </li>
            ))}
          </ul>
        </nav>
      )}
    </div>
  );
}
