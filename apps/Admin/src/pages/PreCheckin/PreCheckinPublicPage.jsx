import { Camera, Car, CheckCircle2, FileText, Plus, Trash2, Users } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { publicPreCheckinsApi } from '@/api/preCheckins';
import { API_BASE_URL } from '@/lib/env';
import { getFriendlyErrorMessage } from '@/lib/uiFeedback';

function dateOnly(value) {
  return value ? String(value).slice(0, 10) : '';
}

function formatDate(value) {
  const normalized = dateOnly(value);
  if (!normalized) return '-';
  const [year, month, day] = normalized.split('-');
  return `${day}/${month}/${year}`;
}

function buildEmptyGuest() {
  return {
    nome: '',
    cpf: '',
    telefone: '',
    email: '',
    dataNascimento: '',
    menorDeIdade: false,
    fotoDocumentoUrl: '',
    uploading: false,
  };
}

function buildEmptyVehicle() {
  return {
    placa: '',
    marca: '',
    modelo: '',
    cor: '',
    observacoes: '',
  };
}

function fileUrl(value) {
  if (!value) return '';
  return value.startsWith('http') ? value : `${API_BASE_URL}${value}`;
}

function toSubmitGuest(hospede) {
  return {
    nome: hospede.nome,
    cpf: hospede.cpf,
    telefone: hospede.telefone,
    email: hospede.email,
    dataNascimento: hospede.dataNascimento,
    menorDeIdade: hospede.menorDeIdade,
    fotoDocumentoUrl: hospede.fotoDocumentoUrl,
  };
}

export function PreCheckinPublicPage() {
  const { token } = useParams();
  const [info, setInfo] = useState(null);
  const [hospedes, setHospedes] = useState([]);
  const [veiculos, setVeiculos] = useState([buildEmptyVehicle()]);
  const [possuiVeiculo, setPossuiVeiculo] = useState(false);
  const [aceitePrivacidade, setAceitePrivacidade] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');
  const [success, setSuccess] = useState('');

  useEffect(() => {
    let active = true;
    const load = async () => {
      setLoading(true);
      setError('');
      try {
        const response = await publicPreCheckinsApi.get(token);
        if (!active) return;
        const data = response.data;
        setInfo(data);
        setHospedes(Array.from({ length: Number(data.numeroHospedes || 1) }, () => buildEmptyGuest()));
      } catch (loadError) {
        if (active) {
          setError(getFriendlyErrorMessage(loadError));
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    };

    load();
    return () => {
      active = false;
    };
  }, [token]);

  const updateHospede = (index, patch) => {
    setHospedes((current) => current.map((hospede, currentIndex) => (currentIndex === index ? { ...hospede, ...patch } : hospede)));
  };

  const updateVeiculo = (index, patch) => {
    setVeiculos((current) => current.map((veiculo, currentIndex) => (currentIndex === index ? { ...veiculo, ...patch } : veiculo)));
  };

  const uploadDocumento = async (index, file) => {
    if (!file) return;
    updateHospede(index, { uploading: true });
    setError('');
    try {
      const response = await publicPreCheckinsApi.uploadPhoto(token, file);
      updateHospede(index, { fotoDocumentoUrl: response.data.url, uploading: false });
    } catch (uploadError) {
      updateHospede(index, { uploading: false });
      setError(getFriendlyErrorMessage(uploadError));
    }
  };

  const submit = async (event) => {
    event.preventDefault();
    setSubmitting(true);
    setError('');
    setSuccess('');

    const payload = {
      aceitePrivacidade,
      hospedes: hospedes.map(toSubmitGuest),
      veiculos: possuiVeiculo ? veiculos : [],
    };

    try {
      await publicPreCheckinsApi.submit(token, payload);
      setSuccess('Cadastro enviado para análise. A administradora vai revisar os dados antes do check-in.');
    } catch (submitError) {
      setError(getFriendlyErrorMessage(submitError));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading) {
    return <div className="public-checkin-shell"><div className="loading-line">Carregando pré-check-in...</div></div>;
  }

  if (error && !info) {
    return (
      <div className="public-checkin-shell">
        <div className="public-checkin-card compact">
          <FileText size={28} />
          <h1>Link indisponível</h1>
          <p>{error}</p>
        </div>
      </div>
    );
  }

  if (success) {
    return (
      <div className="public-checkin-shell">
        <div className="public-checkin-card compact">
          <CheckCircle2 size={32} />
          <h1>Pré-check-in enviado</h1>
          <p>{success}</p>
        </div>
      </div>
    );
  }

  return (
    <div className="public-checkin-shell">
      <form className="public-checkin-card" onSubmit={submit}>
        <section className="public-checkin-heading">
          <span className="eyebrow">Pré-check-in</span>
          <h1>Cadastre os hóspedes da reserva</h1>
          <p>
            {info.imovelNome} · {formatDate(info.checkIn)} até {formatDate(info.checkOut)}
          </p>
          <small>Cadastre exatamente {info.numeroHospedes} hóspede(s). O envio será analisado pela administradora.</small>
        </section>

        {error && <div className="form-alert">{error}</div>}

        <section className="public-checkin-section">
          <div className="form-title">
            <Users size={18} />
            <strong>Hóspedes</strong>
          </div>

          {hospedes.map((hospede, index) => (
            <article className="public-checkin-subcard" key={`hospede-${index}`}>
              <strong>Hóspede {index + 1}</strong>
              <div className="form-grid">
                <label className="form-field">
                  <span>Nome completo</span>
                  <input value={hospede.nome} onChange={(event) => updateHospede(index, { nome: event.target.value })} required />
                </label>
                <label className="form-field">
                  <span>CPF</span>
                  <input value={hospede.cpf} onChange={(event) => updateHospede(index, { cpf: event.target.value })} required />
                </label>
                <label className="form-field">
                  <span>Telefone</span>
                  <input value={hospede.telefone} onChange={(event) => updateHospede(index, { telefone: event.target.value })} />
                </label>
                <label className="form-field">
                  <span>E-mail</span>
                  <input type="email" value={hospede.email} onChange={(event) => updateHospede(index, { email: event.target.value })} />
                </label>
                <label className="form-field">
                  <span>Data de nascimento</span>
                  <input type="date" value={hospede.dataNascimento} onChange={(event) => updateHospede(index, { dataNascimento: event.target.value })} />
                </label>
                <label className="form-field checkbox-field">
                  <input
                    type="checkbox"
                    checked={hospede.menorDeIdade}
                    onChange={(event) => updateHospede(index, { menorDeIdade: event.target.checked })}
                  />
                  <span>Menor de idade</span>
                </label>
                <label className="form-field span-2">
                  <span>Foto ou documento</span>
                  <input type="file" accept="image/png,image/jpeg,image/webp,application/pdf" onChange={(event) => uploadDocumento(index, event.target.files?.[0])} />
                  {hospede.uploading && <small>Enviando arquivo...</small>}
                  {hospede.fotoDocumentoUrl && (
                    <a href={fileUrl(hospede.fotoDocumentoUrl)} target="_blank" rel="noreferrer">
                      Documento enviado
                    </a>
                  )}
                </label>
              </div>
            </article>
          ))}
        </section>

        <section className="public-checkin-section">
          <label className="form-field checkbox-field">
            <input type="checkbox" checked={possuiVeiculo} onChange={(event) => setPossuiVeiculo(event.target.checked)} />
            <span>Algum hóspede vai entrar com veículo?</span>
          </label>

          {possuiVeiculo && (
            <>
              <div className="form-title">
                <Car size={18} />
                <strong>Veículos</strong>
              </div>
              {veiculos.map((veiculo, index) => (
                <article className="public-checkin-subcard" key={`veiculo-${index}`}>
                  <div className="subcard-heading">
                    <strong>Veículo {index + 1}</strong>
                    {veiculos.length > 1 && (
                      <button type="button" onClick={() => setVeiculos((current) => current.filter((_, currentIndex) => currentIndex !== index))}>
                        <Trash2 size={15} />
                        Remover
                      </button>
                    )}
                  </div>
                  <div className="form-grid">
                    <label className="form-field">
                      <span>Placa</span>
                      <input value={veiculo.placa} onChange={(event) => updateVeiculo(index, { placa: event.target.value })} required />
                    </label>
                    <label className="form-field">
                      <span>Marca</span>
                      <input value={veiculo.marca} onChange={(event) => updateVeiculo(index, { marca: event.target.value })} />
                    </label>
                    <label className="form-field">
                      <span>Modelo</span>
                      <input value={veiculo.modelo} onChange={(event) => updateVeiculo(index, { modelo: event.target.value })} />
                    </label>
                    <label className="form-field">
                      <span>Cor</span>
                      <input value={veiculo.cor} onChange={(event) => updateVeiculo(index, { cor: event.target.value })} />
                    </label>
                    <label className="form-field span-2">
                      <span>Observações</span>
                      <textarea value={veiculo.observacoes} onChange={(event) => updateVeiculo(index, { observacoes: event.target.value })} />
                    </label>
                  </div>
                </article>
              ))}
              <button className="secondary-action" type="button" onClick={() => setVeiculos((current) => [...current, buildEmptyVehicle()])}>
                <Plus size={17} />
                Adicionar veículo
              </button>
            </>
          )}
        </section>

        <label className="form-field checkbox-field privacy-accept">
          <input type="checkbox" checked={aceitePrivacidade} onChange={(event) => setAceitePrivacidade(event.target.checked)} required />
          <span>
            Li e aceito a <Link to="/privacidade" target="_blank">política de privacidade</Link> para envio dos dados de hospedagem.
          </span>
        </label>

        <button className="primary-action full" type="submit" disabled={submitting || hospedes.some((h) => h.uploading)}>
          <Camera size={18} />
          {submitting ? 'Enviando...' : 'Enviar para aprovação'}
        </button>
      </form>
    </div>
  );
}
