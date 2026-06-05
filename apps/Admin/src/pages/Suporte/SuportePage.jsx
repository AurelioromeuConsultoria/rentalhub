import { Headphones, LifeBuoy, MessageSquare, RotateCcw, Save } from 'lucide-react';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { sistemaApi } from '@/api/sistema';
import { suporteApi } from '@/api/suporte';
import { useAuth } from '@/context/AuthContext';
import { getFriendlyErrorMessage } from '@/lib/uiFeedback';

const emptyTicket = {
  titulo: '',
  descricao: '',
  modulo: 'geral',
  prioridade: 'media',
};

const statusLabels = {
  aberto: 'Aberto',
  em_atendimento: 'Em atendimento',
  aguardando_cliente: 'Aguardando cliente',
  resolvido: 'Resolvido',
  cancelado: 'Cancelado',
};

const priorityLabels = {
  baixa: 'Baixa',
  media: 'Média',
  alta: 'Alta',
  critica: 'Crítica',
};

function getErrorMessage(error) {
  return getFriendlyErrorMessage(error);
}

function formatDateTime(value) {
  if (!value) {
    return '--';
  }

  return new Intl.DateTimeFormat('pt-BR', {
    dateStyle: 'short',
    timeStyle: 'short',
  }).format(new Date(value));
}

export function SuportePage() {
  const { usuario, canEdit } = useAuth();
  const [tickets, setTickets] = useState([]);
  const [statusFilter, setStatusFilter] = useState('');
  const [form, setForm] = useState(emptyTicket);
  const [supportInfo, setSupportInfo] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const canManageSupport = Boolean(usuario?.isPlatformAdmin || canEdit('configuracoes'));
  const summary = useMemo(() => ({
    abertos: tickets.filter((ticket) => ticket.status === 'aberto').length,
    atendimento: tickets.filter((ticket) => ticket.status === 'em_atendimento').length,
    criticos: tickets.filter((ticket) => ticket.prioridade === 'critica' && !['resolvido', 'cancelado'].includes(ticket.status)).length,
    total: tickets.length,
  }), [tickets]);

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [ticketsResponse, statusResponse] = await Promise.all([
        suporteApi.list(statusFilter ? { status: statusFilter } : {}),
        sistemaApi.status(),
      ]);
      setTickets(ticketsResponse.data || []);
      setSupportInfo(statusResponse.data?.suporte || null);
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  }, [statusFilter]);

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, [load]);

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');
    setSuccess('');

    try {
      await suporteApi.create({
        titulo: form.titulo.trim(),
        descricao: form.descricao.trim(),
        modulo: form.modulo,
        prioridade: form.prioridade,
      });
      setForm(emptyTicket);
      setSuccess('Chamado aberto. Acompanhe o andamento nesta tela.');
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const updateStatus = async (ticket, status) => {
    setError('');
    setSuccess('');
    try {
      await suporteApi.updateStatus(ticket.id, { status });
      setSuccess(`Chamado #${ticket.id} atualizado para ${statusLabels[status] || status}.`);
      await load();
    } catch (updateError) {
      setError(getErrorMessage(updateError));
    }
  };

  return (
    <section className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Atendimento</span>
          <h1>Suporte</h1>
          <p>Registro de chamados, acompanhamento de prioridade e rotina de atendimento do RentalHub.</p>
        </div>
        <div className="resource-actions">
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      {error && <div className="form-alert">{error}</div>}
      {success && <div className="form-success">{success}</div>}

      <div className="kpi-grid secondary-kpis">
        <article className="metric-card">
          <span>Abertos</span>
          <strong>{summary.abertos}</strong>
        </article>
        <article className="metric-card">
          <span>Em atendimento</span>
          <strong>{summary.atendimento}</strong>
        </article>
        <article className="metric-card">
          <span>Críticos ativos</span>
          <strong>{summary.criticos}</strong>
        </article>
        <article className="metric-card">
          <span>Total listado</span>
          <strong>{summary.total}</strong>
        </article>
      </div>

      <section className="resource-panel support-info-panel">
        <div className="resource-panel-heading">
          <div>
            <strong>Canais e janela de atualização</strong>
            <small>Referência operacional para atendimento e atualizações planejadas</small>
          </div>
          <Headphones size={20} />
        </div>
        <div className="status-board">
          <div>
            <small>E-mail</small>
            <strong>{supportInfo?.email || 'Não configurado'}</strong>
          </div>
          <div>
            <small>WhatsApp</small>
            <strong>{supportInfo?.whatsapp || 'Não configurado'}</strong>
          </div>
          <div>
            <small>Horário de suporte</small>
            <strong>{supportInfo?.horario || 'Definir em Configurações'}</strong>
          </div>
          <div>
            <small>Janela de atualização</small>
            <strong>{supportInfo?.janelaAtualizacao || 'Definir em Configurações'}</strong>
          </div>
        </div>
      </section>

      <div className="resource-layout">
        <section className="resource-panel">
          <div className="resource-panel-heading">
            <div>
              <strong>Chamados</strong>
              <small>{canManageSupport ? 'Todos os chamados da empresa' : 'Chamados abertos por você'}</small>
            </div>
            <select className="compact-select" value={statusFilter} onChange={(event) => setStatusFilter(event.target.value)}>
              <option value="">Todos</option>
              {Object.entries(statusLabels).map(([value, label]) => (
                <option value={value} key={value}>{label}</option>
              ))}
            </select>
          </div>

          {loading ? (
            <div className="loading-line">Carregando chamados...</div>
          ) : tickets.length === 0 ? (
            <div className="inline-empty">
              <LifeBuoy size={34} />
              <strong>Nenhum chamado encontrado</strong>
              <span>Abra um chamado com o contexto do problema e a urgência.</span>
            </div>
          ) : (
            <div className="support-ticket-list">
              {tickets.map((ticket) => (
                <article className="support-ticket-card" key={ticket.id}>
                  <div className="support-ticket-main">
                    <div>
                      <strong>#{ticket.id} · {ticket.titulo}</strong>
                      <span>{ticket.descricao}</span>
                    </div>
                    <small>{ticket.createdByNome} · {formatDateTime(ticket.dataCriacao)}</small>
                  </div>
                  <div className="support-ticket-meta">
                    <span className={`status-pill ${ticket.status === 'resolvido' ? 'active' : ticket.status === 'cancelado' ? 'inactive' : 'pending'}`}>
                      {statusLabels[ticket.status] || ticket.status}
                    </span>
                    <span className={`priority-pill ${ticket.prioridade}`}>
                      {priorityLabels[ticket.prioridade] || ticket.prioridade}
                    </span>
                    <small>{ticket.modulo}</small>
                  </div>
                  {canManageSupport && (
                    <div className="button-row support-status-actions">
                      <button type="button" onClick={() => updateStatus(ticket, 'em_atendimento')}>Atender</button>
                      <button type="button" onClick={() => updateStatus(ticket, 'aguardando_cliente')}>Aguardar cliente</button>
                      <button type="button" onClick={() => updateStatus(ticket, 'resolvido')}>Resolver</button>
                    </div>
                  )}
                </article>
              ))}
            </div>
          )}
        </section>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <MessageSquare size={18} />
            <strong>Novo chamado</strong>
          </div>
          <div className="form-grid">
            <label className="form-field span-2">
              <span>Título</span>
              <input value={form.titulo} onChange={(event) => setForm((current) => ({ ...current, titulo: event.target.value }))} required />
            </label>
            <label className="form-field">
              <span>Módulo</span>
              <select value={form.modulo} onChange={(event) => setForm((current) => ({ ...current, modulo: event.target.value }))}>
                <option value="geral">Geral</option>
                <option value="login">Login e acesso</option>
                <option value="reservas">Reservas</option>
                <option value="financeiro">Financeiro</option>
                <option value="repasses">Repasses</option>
                <option value="operacional">Limpeza/manutenção</option>
                <option value="relatorios">Relatórios</option>
              </select>
            </label>
            <label className="form-field">
              <span>Prioridade</span>
              <select value={form.prioridade} onChange={(event) => setForm((current) => ({ ...current, prioridade: event.target.value }))}>
                <option value="baixa">Baixa</option>
                <option value="media">Média</option>
                <option value="alta">Alta</option>
                <option value="critica">Crítica</option>
              </select>
            </label>
            <label className="form-field span-2">
              <span>Descrição</span>
              <textarea
                rows={6}
                value={form.descricao}
                onChange={(event) => setForm((current) => ({ ...current, descricao: event.target.value }))}
                placeholder="Descreva o que aconteceu, em qual tela, qual resultado esperado e se existe cliente impactado."
                required
              />
            </label>
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Abrir chamado'}
          </button>
        </form>
      </div>
    </section>
  );
}
