import { CheckCircle2, ClipboardCheck, Eye, RotateCcw, XCircle } from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { preCheckinsApi } from '@/api/preCheckins';
import { EmptyState } from '@/components/EmptyState';
import { API_BASE_URL } from '@/lib/env';
import { confirmAction, getFriendlyErrorMessage } from '@/lib/uiFeedback';

const statusLabels = {
  1: 'Link gerado',
  2: 'Cadastro enviado',
  3: 'Aprovado',
  4: 'Reprovado',
  5: 'Expirado',
};

function dateOnly(value) {
  return value ? String(value).slice(0, 10) : '';
}

function formatDate(value) {
  const normalized = dateOnly(value);
  if (!normalized) return '-';
  const [year, month, day] = normalized.split('-');
  return `${day}/${month}/${year}`;
}

function statusClass(status) {
  if (Number(status) === 3) return 'active';
  if ([4, 5].includes(Number(status))) return 'inactive';
  return 'pending';
}

function fileUrl(value) {
  if (!value) return '';
  return value.startsWith('http') ? value : `${API_BASE_URL}${value}`;
}

function StatusPill({ status }) {
  return <span className={`status-pill ${statusClass(status)}`}>{statusLabels[status] || '-'}</span>;
}

export function PreCheckinsPage() {
  const [items, setItems] = useState([]);
  const [selected, setSelected] = useState(null);
  const [loading, setLoading] = useState(true);
  const [detailLoading, setDetailLoading] = useState(false);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  const pendingCount = useMemo(() => items.filter((item) => Number(item.status) === 2).length, [items]);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await preCheckinsApi.list();
      setItems(response.data || []);
    } catch (loadError) {
      setError(getFriendlyErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  const loadDetail = async (id) => {
    setDetailLoading(true);
    setError('');
    try {
      const response = await preCheckinsApi.detail(id);
      setSelected(response.data);
    } catch (detailError) {
      setError(getFriendlyErrorMessage(detailError));
    } finally {
      setDetailLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 0);
    return () => clearTimeout(timeout);
  }, []);

  const approve = async () => {
    if (!selected) return;
    const confirmed = confirmAction(
      'Aprovar pré-check-in?',
      'Os hóspedes aprovados serão liberados e cadastrados na base interna quando o CPF ainda não existir.',
    );
    if (!confirmed) return;

    setActionLoading(true);
    setError('');
    setSuccess('');
    try {
      const response = await preCheckinsApi.approve(selected.id);
      setSelected(response.data);
      setSuccess('Pré-check-in aprovado.');
      await load();
    } catch (approveError) {
      setError(getFriendlyErrorMessage(approveError));
    } finally {
      setActionLoading(false);
    }
  };

  const reject = async () => {
    if (!selected) return;
    const motivo = window.prompt('Motivo da reprovação');
    if (motivo === null) return;

    setActionLoading(true);
    setError('');
    setSuccess('');
    try {
      const response = await preCheckinsApi.reject(selected.id, motivo);
      setSelected(response.data);
      setSuccess('Pré-check-in reprovado.');
      await load();
    } catch (rejectError) {
      setError(getFriendlyErrorMessage(rejectError));
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <div className="resource-page">
      <section className="page-heading">
        <div>
          <span className="eyebrow">Operação</span>
          <h1>Pré-check-in</h1>
          <p>Gere links para os hóspedes enviarem CPF, documento e veículos antes da chegada.</p>
        </div>
        <div className="resource-actions">
          <div className="metric-chip">
            <strong>{pendingCount}</strong>
            <span>pendente(s)</span>
          </div>
          <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={load}>
            <RotateCcw size={18} />
          </button>
        </div>
      </section>

      <section className="resource-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <strong>Solicitações</strong>
            <span>{items.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {success && <div className="form-success">{success}</div>}
          {loading ? (
            <div className="loading-line">Carregando pré-check-ins...</div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={<ClipboardCheck size={26} />}
              title="Nenhum pré-check-in gerado"
              description="Gere o link pela tela de reservas para que o hóspede envie os dados antes da chegada."
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Reserva</th>
                    <th>Período</th>
                    <th>Hóspedes</th>
                    <th>Veículos</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.imovelNome}</strong>
                        <small>{item.hospedePrincipalNome}</small>
                      </td>
                      <td>{formatDate(item.checkIn)} até {formatDate(item.checkOut)}</td>
                      <td>{item.totalHospedesCadastrados}/{item.numeroHospedes}</td>
                      <td>{item.totalVeiculosCadastrados}</td>
                      <td><StatusPill status={item.status} /></td>
                      <td className="table-actions">
                        <button type="button" aria-label="Ver detalhes" onClick={() => loadDetail(item.id)}>
                          <Eye size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <aside className="resource-form">
          <div className="form-title">
            <ClipboardCheck size={18} />
            <strong>Detalhes</strong>
          </div>

          {detailLoading ? (
            <div className="loading-line">Carregando detalhes...</div>
          ) : !selected ? (
            <p className="muted-text">Selecione um pré-check-in para revisar hóspedes, documentos e veículos.</p>
          ) : (
            <>
              <div className="detail-summary">
                <strong>{selected.imovelNome}</strong>
                <span>{formatDate(selected.checkIn)} até {formatDate(selected.checkOut)}</span>
                <StatusPill status={selected.status} />
              </div>

              <section className="review-section">
                <strong>Hóspedes</strong>
                {selected.hospedes.length === 0 ? (
                  <p className="muted-text">Nenhum hóspede enviado ainda.</p>
                ) : (
                  selected.hospedes.map((hospede) => (
                    <article className="review-card" key={hospede.id}>
                      <strong>{hospede.nome}</strong>
                      <span>CPF: {hospede.cpf}</span>
                      {hospede.telefone && <span>Telefone: {hospede.telefone}</span>}
                      {hospede.email && <span>E-mail: {hospede.email}</span>}
                      {hospede.dataNascimento && <span>Nascimento: {formatDate(hospede.dataNascimento)}</span>}
                      {hospede.menorDeIdade && <span>Menor de idade</span>}
                      {hospede.fotoDocumentoUrl && (
                        <a href={fileUrl(hospede.fotoDocumentoUrl)} target="_blank" rel="noreferrer">
                          Ver documento
                        </a>
                      )}
                    </article>
                  ))
                )}
              </section>

              <section className="review-section">
                <strong>Veículos</strong>
                {selected.veiculos.length === 0 ? (
                  <p className="muted-text">Nenhum veículo informado.</p>
                ) : (
                  selected.veiculos.map((veiculo) => (
                    <article className="review-card" key={veiculo.id}>
                      <strong>{veiculo.placa}</strong>
                      {[veiculo.marca, veiculo.modelo, veiculo.cor].filter(Boolean).length > 0 && (
                        <span>{[veiculo.marca, veiculo.modelo, veiculo.cor].filter(Boolean).join(' · ')}</span>
                      )}
                      {veiculo.observacoes && <span>{veiculo.observacoes}</span>}
                    </article>
                  ))
                )}
              </section>

              {Number(selected.status) === 2 && (
                <div className="form-actions-row">
                  <button className="primary-action" type="button" disabled={actionLoading} onClick={approve}>
                    <CheckCircle2 size={18} />
                    Aprovar
                  </button>
                  <button className="secondary-action danger" type="button" disabled={actionLoading} onClick={reject}>
                    <XCircle size={18} />
                    Reprovar
                  </button>
                </div>
              )}
            </>
          )}
        </aside>
      </section>
    </div>
  );
}
