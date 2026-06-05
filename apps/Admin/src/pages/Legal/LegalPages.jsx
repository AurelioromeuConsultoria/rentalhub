import { Link } from 'react-router-dom';

const legalVersion = '2026-06-05';

function LegalShell({ title, eyebrow, children }) {
  return (
    <main className="legal-page">
      <section className="legal-document">
        <Link className="legal-back" to="/login">Voltar para login</Link>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        <p className="legal-disclaimer">
          Minuta operacional sujeita a revisão jurídica. Este texto organiza as regras do produto,
          mas deve ser validado por profissional habilitado antes de uso comercial definitivo.
        </p>
        {children}
        <h2>Versão</h2>
        <p>Versão vigente: {legalVersion}.</p>
      </section>
    </main>
  );
}

function LegalList({ items }) {
  return (
    <ul className="legal-list">
      {items.map((item) => (
        <li key={item}>{item}</li>
      ))}
    </ul>
  );
}

export function TermsPage() {
  return (
    <LegalShell eyebrow="RentalHub" title="Termos de Uso">
      <p>
        Estes termos regulam o uso do RentalHub, plataforma SaaS para gestão de imóveis de temporada,
        reservas, hóspedes, proprietários, financeiro, repasses, limpeza, manutenção, relatórios e
        rotinas administrativas.
      </p>

      <h2>1. Conta, empresa e usuários</h2>
      <p>
        Cada cliente opera por uma empresa cadastrada na plataforma. O cliente é responsável pelos
        usuários convidados, permissões concedidas, segurança das credenciais e veracidade das
        informações registradas.
      </p>
      <LegalList
        items={[
          'O acesso é individual e não deve ser compartilhado.',
          'Perfis e permissões devem refletir a função real de cada usuário.',
          'Usuários desligados ou sem necessidade de acesso devem ser removidos pelo cliente.',
        ]}
      />

      <h2>2. Uso permitido</h2>
      <p>
        O RentalHub deve ser usado para a operação lícita de gestão de locações de temporada. O
        cliente declara possuir base legal, autorização ou relação contratual adequada para tratar
        dados de hóspedes, proprietários, usuários e responsáveis operacionais.
      </p>

      <h2>3. Dados inseridos pelo cliente</h2>
      <p>
        O cliente é responsável pelo conteúdo inserido, incluindo cadastros, reservas, documentos,
        valores financeiros, repasses, observações, imagens e demais informações operacionais.
      </p>

      <h2>4. Disponibilidade, suporte e atualizações</h2>
      <p>
        A plataforma poderá receber manutenções, correções e melhorias. Incidentes devem ser
        comunicados pelos canais de suporte definidos comercialmente. Atualizações serão conduzidas
        com esforço razoável para preservar a estabilidade do ambiente.
      </p>

      <h2>5. Limitações de responsabilidade</h2>
      <p>
        O RentalHub apoia a operação e os cálculos, mas o cliente deve conferir informações críticas
        antes de decisões comerciais, fiscais, contábeis ou pagamentos. Valores de repasse,
        relatórios e indicadores dependem dos dados cadastrados pelos usuários.
      </p>

      <h2>6. Suspensão e encerramento</h2>
      <p>
        O acesso poderá ser suspenso em caso de inadimplência, violação destes termos, risco de
        segurança, uso abusivo ou solicitação formal do cliente. Em caso de encerramento, os dados
        poderão ser exportados conforme política comercial e obrigações legais aplicáveis.
      </p>

      <h2>7. Privacidade e proteção de dados</h2>
      <p>
        O tratamento de dados pessoais segue a Política de Privacidade vigente e os recursos de
        exportação, anonimização e auditoria disponíveis no produto.
      </p>
    </LegalShell>
  );
}

export function PrivacyPage() {
  return (
    <LegalShell eyebrow="RentalHub" title="Política de Privacidade">
      <p>
        Esta política descreve como o RentalHub trata dados pessoais para viabilizar a gestão de
        locações de temporada, acesso de usuários, segurança, suporte, auditoria, relatórios e
        obrigações operacionais.
      </p>

      <h2>1. Papéis no tratamento</h2>
      <p>
        Em regra, a empresa cliente atua como controladora dos dados que cadastra na plataforma, e o
        RentalHub atua como operador ao processar esses dados para prestar o serviço contratado.
        Determinadas atividades próprias, como segurança da conta, cobrança, suporte e melhoria do
        produto, podem envolver atuação do RentalHub como controlador.
      </p>

      <h2>2. Dados tratados</h2>
      <LegalList
        items={[
          'Usuários: nome, e-mail, telefone, perfil de acesso, logs e histórico de ações.',
          'Hóspedes: nome, e-mail, telefone, documento, nacionalidade, reservas e observações.',
          'Proprietários: nome, CPF/CNPJ, contato, dados bancários, imóveis, repasses e observações.',
          'Operação: imóveis, fotos, reservas, limpeza, manutenção, despesas, receitas e relatórios.',
          'Dados técnicos: endereço IP, navegador, data/hora, tokens, logs de erro e auditoria.',
        ]}
      />

      <h2>3. Finalidades</h2>
      <LegalList
        items={[
          'Autenticar usuários e proteger contas.',
          'Executar a operação contratada pelo cliente.',
          'Gerar reservas, calendários, relatórios, repasses e demonstrativos.',
          'Prestar suporte, corrigir falhas e investigar incidentes.',
          'Cumprir obrigações legais, regulatórias, fiscais, contábeis e contratuais.',
          'Melhorar segurança, estabilidade e experiência do produto.',
        ]}
      />

      <h2>4. Compartilhamento</h2>
      <p>
        Dados podem ser compartilhados com provedores necessários para hospedagem, banco de dados,
        e-mail, monitoramento, suporte, autenticação, armazenamento e demais serviços essenciais,
        sempre limitados à finalidade do produto e às obrigações contratuais.
      </p>

      <h2>5. Direitos dos titulares</h2>
      <p>
        Titulares podem solicitar confirmação de tratamento, acesso, correção, exportação,
        anonimização, bloqueio ou exclusão, observadas obrigações legais e a necessidade de
        preservação de registros operacionais, financeiros e de auditoria.
      </p>

      <h2>6. Retenção e exclusão</h2>
      <p>
        Dados podem ser mantidos enquanto necessários para operação, suporte, auditoria, cobrança,
        obrigações legais ou defesa de direitos. Quando aplicável, o RentalHub poderá anonimizar
        dados pessoais mantendo informações operacionais indispensáveis ao histórico da empresa.
      </p>

      <h2>7. Segurança</h2>
      <p>
        O RentalHub adota controles de acesso, autenticação, isolamento lógico por empresa,
        auditoria e boas práticas técnicas compatíveis com o estágio do produto. Nenhum sistema é
        imune a riscos; incidentes relevantes serão tratados conforme plano operacional vigente.
      </p>
    </LegalShell>
  );
}

export function ContractPage() {
  return (
    <LegalShell eyebrow="RentalHub" title="Minuta de Contrato SaaS">
      <p>
        Esta minuta resume as condições comerciais esperadas para contratação do RentalHub. A versão
        assinável deve conter dados das partes, plano contratado, preço, vigência, forma de cobrança
        e cláusulas revisadas juridicamente.
      </p>

      <h2>1. Objeto</h2>
      <p>
        Licenciamento de uso do RentalHub em modalidade SaaS para gestão de imóveis de temporada,
        com acesso ao painel administrativo, módulos contratados, suporte e atualizações dentro do
        plano comercial aplicável.
      </p>

      <h2>2. Plano, preço e cobrança</h2>
      <LegalList
        items={[
          'Plano contratado: preencher no pedido comercial.',
          'Quantidade de imóveis, usuários ou limites: preencher conforme proposta.',
          'Valor, vencimento, reajuste e forma de pagamento: preencher conforme proposta.',
          'Inadimplência poderá gerar suspensão após aviso prévio definido em contrato.',
        ]}
      />

      <h2>3. Responsabilidades do cliente</h2>
      <p>
        O cliente deve manter dados corretos, controlar usuários, conferir cálculos relevantes,
        obter autorizações necessárias para tratamento de dados pessoais e cumprir normas aplicáveis
        à sua atividade.
      </p>

      <h2>4. Responsabilidades do fornecedor</h2>
      <p>
        O fornecedor deve disponibilizar a plataforma, aplicar correções e melhorias, preservar
        isolamento lógico entre empresas, manter rotinas razoáveis de segurança e prestar suporte
        conforme canal e prazo definidos comercialmente.
      </p>

      <h2>5. Dados, backup e encerramento</h2>
      <p>
        O contrato deve definir política de backup, prazo de retenção após cancelamento, formato de
        exportação de dados, responsabilidade por restauração e condições de exclusão ou anonimização.
      </p>

      <h2>6. Limitação de responsabilidade</h2>
      <p>
        A versão jurídica deve limitar responsabilidade por perdas indiretas, decisões tomadas com
        dados incorretos cadastrados pelo cliente, falhas de terceiros, indisponibilidades externas e
        eventos fora do controle razoável do fornecedor.
      </p>

      <h2>7. Pontos pendentes para advogado</h2>
      <LegalList
        items={[
          'Foro, lei aplicável e forma de resolução de conflitos.',
          'SLA, suporte e compensações por indisponibilidade, se houver.',
          'Cláusulas específicas de LGPD entre controlador e operador.',
          'Política de cancelamento, multa, reajuste e reembolso.',
          'Anexos comerciais e técnicos.',
        ]}
      />
    </LegalShell>
  );
}
