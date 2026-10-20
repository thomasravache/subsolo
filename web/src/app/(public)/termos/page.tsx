import type { Metadata } from "next";
import Link from "next/link";

import { contactEmail } from "@/components/site/config";
import {
  ContactEmail,
  InstitutionalPage,
} from "@/components/site/InstitutionalPage";

/**
 * SPEC-0028 — `/termos` (no modelo da tela "Privacidade - Subsolo" do Stitch, com os textos do §6): o que é o
 * Subsolo, a conta, quem cadastra shows, o conteúdo proibido, a moderação e as mudanças. Revisão jurídica recomendada
 * antes do go-live.
 */
export const metadata: Metadata = { title: "Termos de uso | Subsolo" };

export const instant = false;

/** Data da última mudança deste texto. */
const updatedAt = "19 de outubro de 2026";

export default async function TermosPage() {
  const email = contactEmail();
  return (
    <InstitutionalPage
      title="Termos de uso"
      updatedAt={updatedAt}
      intro={
        <p>
          Ao usar o <strong>Subsolo</strong> você concorda com estes termos. São
          poucas regras, todas para manter a agenda confiável e a cena segura.
        </p>
      }
      sections={[
        {
          id: "o-que-e",
          title: "O que é o Subsolo",
          content: (
            <p>
              Uma agenda gratuita de shows de metal underground por cidade, sem
              anúncios. Os shows são divulgados por quem faz a cena: bandas,
              casas e produtores. O Subsolo não vende ingressos e não responde
              pela realização dos shows; confira sempre com quem organiza.
            </p>
          ),
        },
        {
          id: "conta",
          title: "Conta",
          content: (
            <ul>
              <li>
                Você entra com um link mágico no seu e-mail ou com o Google. A
                conta é pessoal: não compartilhe o acesso.
              </li>
              <li>
                Você pode excluir a sua conta quando quiser em{" "}
                <Link
                  href="/conta"
                  className="text-text underline underline-offset-4"
                >
                  Minha conta
                </Link>
                . O que fazemos com os seus dados está na{" "}
                <Link
                  href="/privacidade"
                  className="text-text underline underline-offset-4"
                >
                  política de privacidade
                </Link>
                .
              </li>
            </ul>
          ),
        },
        {
          id: "quem-cadastra",
          title: "Quem cadastra shows",
          content: (
            <ul>
              <li>
                Só bandas, casas e produtores com perfil aprovado pela moderação
                cadastram shows.
              </li>
              <li>
                Quem cadastra responde pelas informações: data, local, horário,
                atrações e flyer, e por ter o direito de divulgar o material.
              </li>
              <li>
                Mudou alguma coisa? Atualize o show ou marque como adiado ou
                cancelado.
              </li>
            </ul>
          ),
        },
        {
          id: "conteudo-proibido",
          title: "Conteúdo proibido",
          content: (
            <ul>
              <li>
                Discurso de ódio, de qualquer tipo: racismo, LGBTfobia,
                misoginia, intolerância religiosa e apologia ao nazismo.
              </li>
              <li>
                Bandas, shows e material de NSBM (black metal
                nacional-socialista) ou de qualquer cena que promova o ódio.
              </li>
              <li>
                Show falso, golpe ou divulgação enganosa (data, local ou
                atrações que não existem).
              </li>
              <li>
                Conteúdo ilegal, violento contra pessoas reais ou que viole
                direitos de terceiros.
              </li>
            </ul>
          ),
        },
        {
          id: "moderacao",
          title: "Moderação",
          content: (
            <>
              <p>
                A moderação do Subsolo aprova os perfis e pode recusar, editar
                ou remover perfis e shows que descumpram estes termos, e
                suspender a conta de quem insistir.
              </p>
              <p>
                Viu algo errado? Escreva para <ContactEmail email={email} />.
              </p>
            </>
          ),
        },
        {
          id: "mudancas",
          title: "Mudanças",
          content: (
            <p>
              Estes termos podem mudar com o Subsolo. A data no topo da página
              mostra a última atualização; mudanças importantes são avisadas no
              site antes de valer.
            </p>
          ),
        },
      ]}
    />
  );
}
