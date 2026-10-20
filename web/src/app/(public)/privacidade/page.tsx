import type { Metadata } from "next";
import Link from "next/link";

import { contactEmail } from "@/components/site/config";
import {
  ContactEmail,
  InstitutionalPage,
} from "@/components/site/InstitutionalPage";

/**
 * SPEC-0028 — `/privacidade` (tela "Privacidade - Subsolo" do Stitch, com os textos do §6): o que o Subsolo guarda,
 * para quê, por quanto tempo, os direitos do titular e o canal de contato (`Site:ContactEmail`). O conteúdo segue a
 * tabela de dados pessoais do ADR-0008; revisão jurídica recomendada antes do go-live.
 */
export const metadata: Metadata = { title: "Privacidade | Subsolo" };

export const instant = false;

/** Data da última mudança deste texto. */
const updatedAt = "19 de outubro de 2026";

export default async function PrivacidadePage() {
  const email = contactEmail();
  return (
    <InstitutionalPage
      title="Privacidade"
      updatedAt={updatedAt}
      intro={
        <>
          <p>
            O <strong>Subsolo</strong> é a agenda do metal underground, feita
            pela cena. Aqui está, sem rodeio, o que guardamos sobre você e por
            quê, seguindo a LGPD (Lei 13.709/2018).
          </p>
          <p>
            <strong>
              Não vendemos seus dados, não mostramos anúncios e não usamos
              rastreamento.
            </strong>{" "}
            Os únicos cookies são os necessários para a sua sessão e para a
            segurança dos formulários.
          </p>
        </>
      }
      sections={[
        {
          id: "dados",
          title: "Que dados guardamos",
          content: (
            <ul>
              <li>
                <strong>Conta:</strong> seu e-mail, o nome que você escolher
                (opcional) e, se você entrar com o Google, o identificador da
                sua conta Google.
              </li>
              <li>
                <strong>O que você segue:</strong> as cidades, bandas, casas e
                produtores que você segue no Subsolo.
              </li>
              <li>
                <strong>Avisos:</strong> a inscrição do seu aparelho para
                receber avisos e as suas preferências, só se você ativar.
              </li>
              <li>
                <strong>Perfis de bandas, casas e produtores:</strong> o contato
                informado no pedido do perfil.
              </li>
              <li>
                <strong>Registros técnicos:</strong> o endereço IP e o navegador
                de cada acesso, nos registros do servidor.
              </li>
            </ul>
          ),
        },
        {
          id: "para-que",
          title: "Para quê",
          content: (
            <ul>
              <li>
                <strong>Conta e entrada</strong> (execução do contrato, os{" "}
                <Link
                  href="/termos"
                  className="text-text underline underline-offset-4"
                >
                  termos de uso
                </Link>
                ): para você entrar e usar o Subsolo.
              </li>
              <li>
                <strong>O que você segue</strong> (execução do contrato): para
                montar a sua agenda e mostrar a contagem pública de seguidores,
                que é anônima.
              </li>
              <li>
                <strong>Avisos</strong> (consentimento, que você pode revogar a
                qualquer momento): para mandar os avisos que você pediu.
              </li>
              <li>
                <strong>Contato dos perfis</strong> (execução do contrato): para
                aprovar e moderar bandas, casas e produtores.
              </li>
              <li>
                <strong>Registros técnicos</strong> (legítimo interesse): para
                proteger o Subsolo de abuso e corrigir erros.
              </li>
              <li>
                Nada disso é usado para anúncios nem compartilhado com redes de
                anúncios ou ferramentas de rastreamento.
              </li>
            </ul>
          ),
        },
        {
          id: "tempo",
          title: "Por quanto tempo",
          content: (
            <ul>
              <li>Conta e o que você segue: enquanto a conta existir.</li>
              <li>Avisos: até você desativar ou excluir a conta.</li>
              <li>Contato dos perfis: enquanto o perfil existir.</li>
              <li>Registros técnicos: 30 dias.</li>
              <li>
                Cópias de segurança (backups, criptografados): os dados
                excluídos somem delas em até 6 meses.
              </li>
            </ul>
          ),
        },
        {
          id: "direitos",
          title: "Seus direitos",
          content: (
            <>
              <p>
                A LGPD garante a você saber o que guardamos, corrigir, levar os
                seus dados, revogar o consentimento e pedir a exclusão.
              </p>
              <ul>
                <li>
                  Em{" "}
                  <Link
                    href="/conta"
                    className="text-text underline underline-offset-4"
                  >
                    Minha conta
                  </Link>{" "}
                  você exporta os seus dados num arquivo e exclui a sua conta,
                  sem precisar pedir a ninguém.
                </li>
                <li>
                  Para qualquer outro pedido ou dúvida, fale com a gente pelo
                  contato abaixo.
                </li>
              </ul>
            </>
          ),
        },
        {
          id: "contato",
          title: "Contato",
          content: (
            <>
              <p>
                O canal do titular dos dados é o e-mail{" "}
                <ContactEmail email={email} />. Respondemos em até 15 dias.
              </p>
              <p>
                Se acontecer um incidente de segurança com risco para você,
                avisamos você e a Autoridade Nacional de Proteção de Dados
                (ANPD).
              </p>
            </>
          ),
        },
      ]}
    />
  );
}
