import { Link } from 'react-router-dom';

function LegalShell({ title, eyebrow, children }) {
  return (
    <main className="legal-page">
      <section className="legal-document">
        <Link className="legal-back" to="/login">Voltar para login</Link>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        {children}
      </section>
    </main>
  );
}

export function TermsPage() {
  return (
    <LegalShell eyebrow="RentalHub" title="Termos de Uso">
      <p>
        Estes termos regulam o uso do RentalHub, plataforma SaaS para gestão de imóveis de temporada,
        reservas, hóspedes, proprietários, financeiro, repasses e rotinas operacionais.
      </p>
      <h2>Uso da plataforma</h2>
      <p>
        Cada empresa é responsável pelos dados inseridos, pelos usuários convidados e pela operação
        feita em seu ambiente. O RentalHub mantém isolamento lógico entre empresas e registra eventos
        relevantes para auditoria.
      </p>
      <h2>Responsabilidades</h2>
      <p>
        O cliente deve usar informações verdadeiras, manter credenciais seguras, respeitar leis
        aplicáveis e possuir autorização para tratar dados de hóspedes, proprietários e usuários.
      </p>
      <h2>Disponibilidade e suporte</h2>
      <p>
        A plataforma pode passar por manutenções, atualizações e melhorias. Incidentes operacionais
        devem ser comunicados pelos canais oficiais de suporte definidos comercialmente.
      </p>
      <h2>Versão</h2>
      <p>Versão vigente: 2026-06-04.</p>
    </LegalShell>
  );
}

export function PrivacyPage() {
  return (
    <LegalShell eyebrow="RentalHub" title="Política de Privacidade">
      <p>
        O RentalHub trata dados pessoais para permitir a gestão de locações de temporada, incluindo
        cadastros, reservas, financeiro, repasses, acesso de usuários e auditoria da operação.
      </p>
      <h2>Dados tratados</h2>
      <p>
        Podemos tratar nome, e-mail, telefone, documento, nacionalidade, dados bancários de
        proprietários, histórico de reservas, registros operacionais e logs técnicos.
      </p>
      <h2>Finalidade</h2>
      <p>
        Os dados são usados para autenticação, execução da operação contratada, suporte, segurança,
        auditoria, obrigações legais e melhoria do produto.
      </p>
      <h2>Direitos do titular</h2>
      <p>
        Titulares podem solicitar acesso, correção, exportação ou anonimização/exclusão de dados,
        observadas obrigações legais, contábeis e operacionais que exijam manutenção de registros.
      </p>
      <h2>Retenção</h2>
      <p>
        Registros financeiros e reservas podem ser preservados para histórico e obrigações legais.
        Quando aplicável, os dados pessoais são anonimizados sem remover a rastreabilidade operacional.
      </p>
      <h2>Versão</h2>
      <p>Versão vigente: 2026-06-04.</p>
    </LegalShell>
  );
}
