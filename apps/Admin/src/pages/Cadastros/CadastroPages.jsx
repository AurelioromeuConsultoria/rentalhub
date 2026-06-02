import {
  Building2,
  Edit3,
  Image as ImageIcon,
  Plus,
  RotateCcw,
  Save,
  Search,
  Star,
  Trash2,
  UploadCloud,
  UserRound,
  Users,
  X,
} from 'lucide-react';
import { useEffect, useMemo, useState } from 'react';
import { hospedesApi, imoveisApi, proprietariosApi } from '@/api/cadastros';

const imovelStatusOptions = [
  { value: 1, label: 'Ativo' },
  { value: 2, label: 'Inativo' },
  { value: 3, label: 'Em manutenção' },
];

const IBGE_BASE_URL = 'https://servicodados.ibge.gov.br/api/v1/localidades';
const VIACEP_BASE_URL = 'https://viacep.com.br/ws';

const emptyProprietario = {
  nome: '',
  documento: '',
  telefone: '',
  email: '',
  dadosBancarios: '',
  observacoes: '',
  ativo: true,
};

const emptyHospede = {
  nome: '',
  email: '',
  telefone: '',
  documento: '',
  nacionalidade: 'Brasil',
  observacoes: '',
  ativo: true,
};

const emptyImovel = {
  proprietarioId: '',
  nome: '',
  codigoInterno: '',
  descricao: '',
  enderecoLogradouro: '',
  enderecoNumero: '',
  enderecoBairro: '',
  enderecoComplemento: '',
  cidade: '',
  estado: '',
  cep: '',
  quantidadeHospedes: 1,
  quantidadeQuartos: 1,
  quantidadeBanheiros: 1,
  status: 1,
  comodidadesTexto: '',
  fotos: [],
};

function normalizeText(value) {
  return value?.trim() || '';
}

function onlyDigits(value) {
  return String(value || '').replace(/\D/g, '');
}

function formatCep(value) {
  const digits = onlyDigits(value).slice(0, 8);
  if (digits.length <= 5) {
    return digits;
  }

  return `${digits.slice(0, 5)}-${digits.slice(5)}`;
}

function composeEndereco({ enderecoLogradouro, enderecoNumero, enderecoBairro, enderecoComplemento }) {
  const linha = [enderecoLogradouro, enderecoNumero].map(normalizeText).filter(Boolean).join(', ');
  return [linha, enderecoBairro, enderecoComplemento].map(normalizeText).filter(Boolean).join(' - ');
}

function parseEndereco(endereco) {
  const [linha = '', bairro = '', complemento = ''] = String(endereco || '').split(' - ');
  const [logradouro = '', ...numeroParts] = linha.split(', ');

  return {
    enderecoLogradouro: logradouro.trim(),
    enderecoNumero: numeroParts.join(', ').trim(),
    enderecoBairro: bairro.trim(),
    enderecoComplemento: complemento.trim(),
  };
}

async function fetchEstados() {
  const response = await fetch(`${IBGE_BASE_URL}/estados?orderBy=nome`);
  if (!response.ok) {
    throw new Error('Não foi possível carregar estados.');
  }

  return response.json();
}

async function fetchCidades(uf) {
  const response = await fetch(`${IBGE_BASE_URL}/estados/${uf}/municipios?orderBy=nome`);
  if (!response.ok) {
    throw new Error('Não foi possível carregar cidades.');
  }

  return response.json();
}

async function fetchCep(cep) {
  const digits = onlyDigits(cep);
  const response = await fetch(`${VIACEP_BASE_URL}/${digits}/json/`);
  if (!response.ok) {
    throw new Error('Não foi possível consultar o CEP.');
  }

  const data = await response.json();
  if (data.erro) {
    throw new Error('CEP não encontrado.');
  }

  return data;
}

function extractItems(response) {
  return response.data?.items || [];
}

function getErrorMessage(error) {
  return error.response?.data?.message || 'Não foi possível concluir a operação.';
}

function normalizeFoto(foto, index) {
  return {
    id: foto.id || `local-${index}-${Date.now()}`,
    url: foto.url || '',
    descricao: foto.descricao || '',
    ordem: foto.ordem || index + 1,
    principal: Boolean(foto.principal || index === 0),
  };
}

function normalizeFotos(fotos = []) {
  const normalized = fotos
    .filter((foto) => foto?.url)
    .map((foto, index) => normalizeFoto(foto, index));

  if (normalized.length > 0 && !normalized.some((foto) => foto.principal)) {
    normalized[0].principal = true;
  }

  return normalized.map((foto, index) => ({ ...foto, ordem: index + 1 }));
}

function StatusPill({ active, label }) {
  return <span className={`status-pill ${active ? 'active' : 'inactive'}`}>{label}</span>;
}

function ResourceHeader({ eyebrow, title, description, onCreate, onRefresh }) {
  return (
    <section className="page-heading">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <div className="resource-actions">
        <button className="icon-button bordered" type="button" aria-label="Atualizar" onClick={onRefresh}>
          <RotateCcw size={18} />
        </button>
        <button className="primary-action" type="button" onClick={onCreate}>
          <Plus size={18} />
          Novo
        </button>
      </div>
    </section>
  );
}

function SearchBar({ value, onChange, placeholder }) {
  return (
    <label className="search-field">
      <Search size={18} />
      <input value={value} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />
    </label>
  );
}

function EmptyState({ title, description, icon }) {
  return (
    <div className="inline-empty">
      {icon}
      <strong>{title}</strong>
      <span>{description}</span>
    </div>
  );
}

function TextField({ label, value, onChange, onBlur, required, type = 'text', placeholder }) {
  return (
    <label className="form-field">
      <span>{label}</span>
      <input
        type={type}
        value={value ?? ''}
        onChange={(event) => onChange(event.target.value)}
        onBlur={onBlur}
        placeholder={placeholder}
        required={required}
      />
    </label>
  );
}

function TextAreaField({ label, value, onChange, placeholder }) {
  return (
    <label className="form-field span-2">
      <span>{label}</span>
      <textarea value={value ?? ''} onChange={(event) => onChange(event.target.value)} placeholder={placeholder} />
    </label>
  );
}

function CheckboxField({ label, checked, onChange }) {
  return (
    <label className="checkbox-field">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <span>{label}</span>
    </label>
  );
}

function FotosField({ fotos, uploading, onUpload, onChange }) {
  const updateFoto = (id, patch) => {
    onChange(fotos.map((foto) => (foto.id === id ? { ...foto, ...patch } : foto)));
  };

  const removeFoto = (id) => {
    onChange(normalizeFotos(fotos.filter((foto) => foto.id !== id)));
  };

  const setPrincipal = (id) => {
    onChange(fotos.map((foto) => ({ ...foto, principal: foto.id === id })));
  };

  return (
    <div className="form-field span-2">
      <span>Fotos</span>
      <label className="photo-upload-dropzone">
        <UploadCloud size={20} />
        <strong>{uploading ? 'Enviando foto...' : 'Enviar fotos do imóvel'}</strong>
        <small>JPG, PNG ou WebP até 8MB</small>
        <input type="file" accept="image/jpeg,image/png,image/webp" multiple disabled={uploading} onChange={onUpload} />
      </label>

      {fotos.length === 0 ? (
        <div className="inline-empty compact">
          <ImageIcon size={24} />
          <strong>Nenhuma foto adicionada</strong>
          <span>Adicione imagens reais do imóvel para facilitar a operação e conferência.</span>
        </div>
      ) : (
        <div className="photo-grid">
          {fotos.map((foto) => (
            <article className="photo-card" key={foto.id}>
              <img src={foto.url} alt={foto.descricao || 'Foto do imóvel'} />
              <div className="photo-card-actions">
                <button
                  type="button"
                  className={foto.principal ? 'active' : ''}
                  title="Definir como capa"
                  onClick={() => setPrincipal(foto.id)}
                >
                  <Star size={15} />
                </button>
                <button type="button" title="Remover foto" onClick={() => removeFoto(foto.id)}>
                  <X size={15} />
                </button>
              </div>
              <input
                value={foto.descricao}
                placeholder="Descrição da foto"
                onChange={(event) => updateFoto(foto.id, { descricao: event.target.value })}
              />
            </article>
          ))}
        </div>
      )}
    </div>
  );
}

export function ProprietariosPage() {
  const [items, setItems] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyProprietario);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await proprietariosApi.list({ search });
      setItems(extractItems(response));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 250);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const startCreate = () => {
    setEditingId(null);
    setForm(emptyProprietario);
  };

  const startEdit = (item) => {
    setEditingId(item.id);
    setForm({
      nome: item.nome || '',
      documento: item.documento || '',
      telefone: item.telefone || '',
      email: item.email || '',
      dadosBancarios: item.dadosBancarios || '',
      observacoes: item.observacoes || '',
      ativo: item.ativo,
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    const payload = {
      ...form,
      nome: normalizeText(form.nome),
      documento: normalizeText(form.documento),
      telefone: normalizeText(form.telefone),
      email: normalizeText(form.email),
      dadosBancarios: normalizeText(form.dadosBancarios),
      observacoes: normalizeText(form.observacoes),
    };

    try {
      if (editingId) {
        await proprietariosApi.update(editingId, payload);
      } else {
        await proprietariosApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (item) => {
    setError('');
    try {
      await proprietariosApi.deactivate(item.id);
      await load();
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <div className="resource-page">
      <ResourceHeader
        eyebrow="Cadastros base"
        title="Proprietários"
        description="Cadastro dos donos dos imóveis, dados bancários e vínculo operacional para repasses."
        onCreate={startCreate}
        onRefresh={load}
      />

      <section className="resource-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <SearchBar value={search} onChange={setSearch} placeholder="Buscar por nome, documento ou e-mail" />
            <span>{items.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {loading ? (
            <div className="loading-line">Carregando proprietários...</div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={<Users size={26} />}
              title="Nenhum proprietário cadastrado"
              description="Cadastre o primeiro proprietário para liberar o cadastro de imóveis."
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Nome</th>
                    <th>Documento</th>
                    <th>Contato</th>
                    <th>Imóveis</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.nome}</strong>
                        <small>{item.email || 'Sem e-mail'}</small>
                      </td>
                      <td>{item.documento}</td>
                      <td>{item.telefone || '-'}</td>
                      <td>{item.totalImoveis}</td>
                      <td>
                        <StatusPill active={item.ativo} label={item.ativo ? 'Ativo' : 'Inativo'} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(item)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Inativar" onClick={() => deactivate(item)}>
                          <Trash2 size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <Plus size={18} />
            <strong>{editingId ? 'Editar proprietário' : 'Novo proprietário'}</strong>
          </div>
          <div className="form-grid">
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField
              label="CPF/CNPJ"
              value={form.documento}
              onChange={(documento) => setForm((current) => ({ ...current, documento }))}
              required
            />
            <TextField label="Telefone" value={form.telefone} onChange={(telefone) => setForm((current) => ({ ...current, telefone }))} />
            <TextField label="E-mail" type="email" value={form.email} onChange={(email) => setForm((current) => ({ ...current, email }))} />
            <TextAreaField
              label="Dados bancários"
              value={form.dadosBancarios}
              onChange={(dadosBancarios) => setForm((current) => ({ ...current, dadosBancarios }))}
            />
            <TextAreaField
              label="Observações"
              value={form.observacoes}
              onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))}
            />
            <CheckboxField label="Proprietário ativo" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar proprietário'}
          </button>
        </form>
      </section>
    </div>
  );
}

export function HospedesPage() {
  const [items, setItems] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyHospede);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const response = await hospedesApi.list({ search });
      setItems(extractItems(response));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 250);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  const startCreate = () => {
    setEditingId(null);
    setForm(emptyHospede);
  };

  const startEdit = (item) => {
    setEditingId(item.id);
    setForm({
      nome: item.nome || '',
      email: item.email || '',
      telefone: item.telefone || '',
      documento: item.documento || '',
      nacionalidade: item.nacionalidade || '',
      observacoes: item.observacoes || '',
      ativo: item.ativo,
    });
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    const payload = {
      ...form,
      nome: normalizeText(form.nome),
      email: normalizeText(form.email),
      telefone: normalizeText(form.telefone),
      documento: normalizeText(form.documento),
      nacionalidade: normalizeText(form.nacionalidade),
      observacoes: normalizeText(form.observacoes),
    };

    try {
      if (editingId) {
        await hospedesApi.update(editingId, payload);
      } else {
        await hospedesApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (item) => {
    setError('');
    try {
      await hospedesApi.deactivate(item.id);
      await load();
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <div className="resource-page">
      <ResourceHeader
        eyebrow="Cadastros base"
        title="Hóspedes"
        description="Base de hóspedes com documento, contato, nacionalidade e observações para histórico de reservas."
        onCreate={startCreate}
        onRefresh={load}
      />

      <section className="resource-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <SearchBar value={search} onChange={setSearch} placeholder="Buscar por nome, documento ou e-mail" />
            <span>{items.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {loading ? (
            <div className="loading-line">Carregando hóspedes...</div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={<UserRound size={26} />}
              title="Nenhum hóspede cadastrado"
              description="Cadastre hóspedes para criar reservas na próxima sprint."
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Nome</th>
                    <th>Documento</th>
                    <th>Contato</th>
                    <th>Nacionalidade</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.id}>
                      <td>
                        <strong>{item.nome}</strong>
                        <small>{item.email || 'Sem e-mail'}</small>
                      </td>
                      <td>{item.documento || '-'}</td>
                      <td>{item.telefone || '-'}</td>
                      <td>{item.nacionalidade || '-'}</td>
                      <td>
                        <StatusPill active={item.ativo} label={item.ativo ? 'Ativo' : 'Inativo'} />
                      </td>
                      <td className="table-actions">
                        <button type="button" aria-label="Editar" onClick={() => startEdit(item)}>
                          <Edit3 size={16} />
                        </button>
                        <button type="button" aria-label="Inativar" onClick={() => deactivate(item)}>
                          <Trash2 size={16} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <Plus size={18} />
            <strong>{editingId ? 'Editar hóspede' : 'Novo hóspede'}</strong>
          </div>
          <div className="form-grid">
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField label="E-mail" type="email" value={form.email} onChange={(email) => setForm((current) => ({ ...current, email }))} />
            <TextField label="Telefone" value={form.telefone} onChange={(telefone) => setForm((current) => ({ ...current, telefone }))} />
            <TextField label="Documento" value={form.documento} onChange={(documento) => setForm((current) => ({ ...current, documento }))} />
            <TextField
              label="Nacionalidade"
              value={form.nacionalidade}
              onChange={(nacionalidade) => setForm((current) => ({ ...current, nacionalidade }))}
            />
            <TextAreaField
              label="Observações"
              value={form.observacoes}
              onChange={(observacoes) => setForm((current) => ({ ...current, observacoes }))}
            />
            <CheckboxField label="Hóspede ativo" checked={form.ativo} onChange={(ativo) => setForm((current) => ({ ...current, ativo }))} />
          </div>
          <button className="primary-action full" type="submit" disabled={saving}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar hóspede'}
          </button>
        </form>
      </section>
    </div>
  );
}

export function ImoveisPage() {
  const [items, setItems] = useState([]);
  const [proprietarios, setProprietarios] = useState([]);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState(emptyImovel);
  const [editingId, setEditingId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [photoUploading, setPhotoUploading] = useState(false);
  const [cepLoading, setCepLoading] = useState(false);
  const [addressLoading, setAddressLoading] = useState(true);
  const [addressMessage, setAddressMessage] = useState('');
  const [estados, setEstados] = useState([]);
  const [cidades, setCidades] = useState([]);
  const [error, setError] = useState('');

  const proprietarioOptions = useMemo(() => proprietarios.filter((proprietario) => proprietario.ativo), [proprietarios]);

  const load = async () => {
    setLoading(true);
    setError('');
    try {
      const [imoveisResponse, proprietariosResponse] = await Promise.all([
        imoveisApi.list({ search }),
        proprietariosApi.list({ ativo: true, pageSize: 100 }),
      ]);
      setItems(extractItems(imoveisResponse));
      setProprietarios(extractItems(proprietariosResponse));
    } catch (loadError) {
      setError(getErrorMessage(loadError));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    const timeout = setTimeout(load, 250);
    return () => clearTimeout(timeout);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [search]);

  useEffect(() => {
    let active = true;

    fetchEstados()
      .then((data) => {
        if (active) {
          setEstados(data.map((estado) => ({ id: estado.id, sigla: estado.sigla, nome: estado.nome })));
        }
      })
      .catch(() => {
        if (active) {
          setEstados([]);
          setAddressMessage('Não foi possível carregar a lista de estados. Preencha manualmente.');
        }
      })
      .finally(() => {
        if (active) {
          setAddressLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    let active = true;
    const uf = form.estado;

    if (!uf) {
      return undefined;
    }

    Promise.resolve()
      .then(() => {
        if (active) {
          setAddressLoading(true);
        }

        return fetchCidades(uf);
      })
      .then((data) => {
        if (active) {
          setCidades(data.map((cidade) => ({ id: cidade.id, nome: cidade.nome })));
        }
      })
      .catch(() => {
        if (active) {
          setCidades([]);
          setAddressMessage('Não foi possível carregar cidades para este estado. Preencha manualmente.');
        }
      })
      .finally(() => {
        if (active) {
          setAddressLoading(false);
        }
      });

    return () => {
      active = false;
    };
  }, [form.estado]);

  const updateEstado = (estado) => {
    setAddressMessage('');
    setForm((current) => ({
      ...current,
      estado,
      cidade: estado === current.estado ? current.cidade : '',
    }));
    if (!estado || estado !== form.estado) {
      setCidades([]);
    }
  };

  const lookupCep = async () => {
    const cepDigits = onlyDigits(form.cep);
    if (cepDigits.length !== 8) {
      return;
    }

    setCepLoading(true);
    setAddressMessage('');

    try {
      const data = await fetchCep(cepDigits);

      setForm((current) => ({
        ...current,
        cep: formatCep(data.cep || cepDigits),
        estado: data.uf || current.estado,
        cidade: data.localidade || current.cidade,
        enderecoLogradouro: data.logradouro || current.enderecoLogradouro,
        enderecoBairro: data.bairro || current.enderecoBairro,
        enderecoComplemento: data.complemento || current.enderecoComplemento,
      }));
    } catch (cepError) {
      setAddressMessage(cepError.message || 'Não foi possível consultar o CEP.');
    } finally {
      setCepLoading(false);
    }
  };

  const startCreate = () => {
    setEditingId(null);
    setForm({
      ...emptyImovel,
      proprietarioId: proprietarioOptions[0]?.id ? String(proprietarioOptions[0].id) : '',
    });
  };

  const startEdit = (item) => {
    const enderecoFields = parseEndereco(item.endereco);

    setEditingId(item.id);
    setForm({
      proprietarioId: String(item.proprietarioId),
      nome: item.nome || '',
      codigoInterno: item.codigoInterno || '',
      descricao: item.descricao || '',
      ...enderecoFields,
      cidade: item.cidade || '',
      estado: item.estado || '',
      cep: item.cep || '',
      quantidadeHospedes: item.quantidadeHospedes || 1,
      quantidadeQuartos: item.quantidadeQuartos || 0,
      quantidadeBanheiros: item.quantidadeBanheiros || 0,
      status: item.status || 1,
      comodidadesTexto: (item.comodidades || []).join(', '),
      fotos: normalizeFotos(item.fotos || []),
    });
  };

  const uploadFotos = async (event) => {
    const files = Array.from(event.target.files || []);
    if (files.length === 0) {
      return;
    }

    setPhotoUploading(true);
    setError('');

    try {
      const uploaded = [];
      for (const file of files) {
        const response = await imoveisApi.uploadFoto(file);
        uploaded.push({
          url: response.data.url,
          descricao: file.name.replace(/\.[^/.]+$/, ''),
        });
      }

      setForm((current) => ({
        ...current,
        fotos: normalizeFotos([...current.fotos, ...uploaded]),
      }));
    } catch (uploadError) {
      setError(getErrorMessage(uploadError));
    } finally {
      setPhotoUploading(false);
      event.target.value = '';
    }
  };

  const save = async (event) => {
    event.preventDefault();
    setSaving(true);
    setError('');

    const payload = {
      proprietarioId: Number(form.proprietarioId),
      nome: normalizeText(form.nome),
      codigoInterno: normalizeText(form.codigoInterno),
      descricao: normalizeText(form.descricao),
      endereco: composeEndereco(form),
      cidade: normalizeText(form.cidade),
      estado: normalizeText(form.estado),
      cep: normalizeText(form.cep),
      quantidadeHospedes: Number(form.quantidadeHospedes),
      quantidadeQuartos: Number(form.quantidadeQuartos),
      quantidadeBanheiros: Number(form.quantidadeBanheiros),
      status: Number(form.status),
      comodidades: form.comodidadesTexto
        .split(',')
        .map((comodidade) => comodidade.trim())
        .filter(Boolean),
      fotos: normalizeFotos(form.fotos).map((foto, index) => ({
        url: foto.url,
        descricao: normalizeText(foto.descricao),
        ordem: index + 1,
        principal: foto.principal || index === 0,
      })),
    };

    try {
      if (editingId) {
        await imoveisApi.update(editingId, payload);
      } else {
        await imoveisApi.create(payload);
      }
      startCreate();
      await load();
    } catch (saveError) {
      setError(getErrorMessage(saveError));
    } finally {
      setSaving(false);
    }
  };

  const deactivate = async (item) => {
    setError('');
    try {
      await imoveisApi.deactivate(item.id);
      await load();
    } catch (deactivateError) {
      setError(getErrorMessage(deactivateError));
    }
  };

  return (
    <div className="resource-page">
      <ResourceHeader
        eyebrow="Cadastros base"
        title="Imóveis"
        description="Cadastro operacional dos imóveis, vínculo com proprietário, capacidades, status, comodidades e fotos."
        onCreate={startCreate}
        onRefresh={load}
      />

      <section className="resource-layout">
        <article className="resource-panel">
          <div className="resource-panel-heading">
            <SearchBar value={search} onChange={setSearch} placeholder="Buscar por nome, código ou cidade" />
            <span>{items.length} registros</span>
          </div>
          {error && <div className="form-alert">{error}</div>}
          {loading ? (
            <div className="loading-line">Carregando imóveis...</div>
          ) : items.length === 0 ? (
            <EmptyState
              icon={<Building2 size={26} />}
              title="Nenhum imóvel cadastrado"
              description="Cadastre proprietários e depois os imóveis vinculados a eles."
            />
          ) : (
            <div className="data-table-wrap">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Imóvel</th>
                    <th>Proprietário</th>
                    <th>Cidade</th>
                    <th>Capacidade</th>
                    <th>Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => {
                    const statusLabel = imovelStatusOptions.find((option) => option.value === item.status)?.label || 'Status';
                    const cover = item.fotos?.find((foto) => foto.principal) || item.fotos?.[0];
                    return (
                      <tr key={item.id}>
                        <td>
                          <div className="property-cell">
                            {cover?.url ? (
                              <img src={cover.url} alt={item.nome} />
                            ) : (
                              <span><ImageIcon size={17} /></span>
                            )}
                            <div>
                              <strong>{item.nome}</strong>
                              <small>{item.codigoInterno}</small>
                            </div>
                          </div>
                        </td>
                        <td>{item.proprietarioNome}</td>
                        <td>{[item.cidade, item.estado].filter(Boolean).join(' / ') || '-'}</td>
                        <td>{item.quantidadeHospedes} hóspedes</td>
                        <td>
                          <StatusPill active={item.status === 1} label={statusLabel} />
                        </td>
                        <td className="table-actions">
                          <button type="button" aria-label="Editar" onClick={() => startEdit(item)}>
                            <Edit3 size={16} />
                          </button>
                          <button type="button" aria-label="Inativar" onClick={() => deactivate(item)}>
                            <Trash2 size={16} />
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </article>

        <form className="resource-form" onSubmit={save}>
          <div className="form-title">
            <Plus size={18} />
            <strong>{editingId ? 'Editar imóvel' : 'Novo imóvel'}</strong>
          </div>
          <div className="form-grid">
            <label className="form-field span-2">
              <span>Proprietário</span>
              <select
                value={form.proprietarioId}
                onChange={(event) => setForm((current) => ({ ...current, proprietarioId: event.target.value }))}
                required
              >
                <option value="">Selecione</option>
                {proprietarioOptions.map((proprietario) => (
                  <option key={proprietario.id} value={proprietario.id}>
                    {proprietario.nome}
                  </option>
                ))}
              </select>
            </label>
            <TextField label="Nome" value={form.nome} onChange={(nome) => setForm((current) => ({ ...current, nome }))} required />
            <TextField
              label="Código interno"
              value={form.codigoInterno}
              onChange={(codigoInterno) => setForm((current) => ({ ...current, codigoInterno }))}
              required
            />
            <TextField
              label={cepLoading ? 'CEP consultando...' : 'CEP'}
              value={form.cep}
              onChange={(cep) => {
                setAddressMessage('');
                setForm((current) => ({ ...current, cep: formatCep(cep) }));
              }}
              onBlur={lookupCep}
              placeholder="00000-000"
            />
            <TextField
              label="Logradouro"
              value={form.enderecoLogradouro}
              onChange={(enderecoLogradouro) => setForm((current) => ({ ...current, enderecoLogradouro }))}
            />
            <TextField
              label="Número"
              value={form.enderecoNumero}
              onChange={(enderecoNumero) => setForm((current) => ({ ...current, enderecoNumero }))}
            />
            <TextField
              label="Bairro"
              value={form.enderecoBairro}
              onChange={(enderecoBairro) => setForm((current) => ({ ...current, enderecoBairro }))}
            />
            <TextField
              label="Complemento"
              value={form.enderecoComplemento}
              onChange={(enderecoComplemento) => setForm((current) => ({ ...current, enderecoComplemento }))}
            />
            <label className="form-field">
              <span>Estado</span>
              <select value={form.estado} onChange={(event) => updateEstado(event.target.value)} disabled={addressLoading && estados.length === 0}>
                <option value="">{addressLoading && estados.length === 0 ? 'Carregando...' : 'Selecione'}</option>
                {estados.map((estado) => (
                  <option key={estado.id} value={estado.sigla}>
                    {estado.sigla} - {estado.nome}
                  </option>
                ))}
              </select>
            </label>
            <label className="form-field">
              <span>Cidade</span>
              <select
                value={form.cidade}
                onChange={(event) => setForm((current) => ({ ...current, cidade: event.target.value }))}
                disabled={!form.estado || addressLoading}
              >
                <option value="">{form.estado ? 'Selecione' : 'Escolha o estado'}</option>
                {form.cidade && !cidades.some((cidade) => cidade.nome === form.cidade) && (
                  <option value={form.cidade}>{form.cidade}</option>
                )}
                {cidades.map((cidade) => (
                  <option key={cidade.id} value={cidade.nome}>
                    {cidade.nome}
                  </option>
                ))}
              </select>
            </label>
            {addressMessage && <div className="form-hint span-2">{addressMessage}</div>}
            <TextField
              label="Hóspedes"
              type="number"
              value={form.quantidadeHospedes}
              onChange={(quantidadeHospedes) => setForm((current) => ({ ...current, quantidadeHospedes }))}
              required
            />
            <TextField
              label="Quartos"
              type="number"
              value={form.quantidadeQuartos}
              onChange={(quantidadeQuartos) => setForm((current) => ({ ...current, quantidadeQuartos }))}
            />
            <TextField
              label="Banheiros"
              type="number"
              value={form.quantidadeBanheiros}
              onChange={(quantidadeBanheiros) => setForm((current) => ({ ...current, quantidadeBanheiros }))}
            />
            <label className="form-field">
              <span>Status</span>
              <select value={form.status} onChange={(event) => setForm((current) => ({ ...current, status: Number(event.target.value) }))}>
                {imovelStatusOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <TextAreaField label="Descrição" value={form.descricao} onChange={(descricao) => setForm((current) => ({ ...current, descricao }))} />
            <TextAreaField
              label="Comodidades"
              value={form.comodidadesTexto}
              onChange={(comodidadesTexto) => setForm((current) => ({ ...current, comodidadesTexto }))}
              placeholder="Wi-Fi, piscina, churrasqueira"
            />
            <FotosField
              fotos={form.fotos}
              uploading={photoUploading}
              onUpload={uploadFotos}
              onChange={(fotos) => setForm((current) => ({ ...current, fotos: normalizeFotos(fotos) }))}
            />
          </div>
          <button className="primary-action full" type="submit" disabled={saving || proprietarioOptions.length === 0}>
            <Save size={18} />
            {saving ? 'Salvando...' : 'Salvar imóvel'}
          </button>
        </form>
      </section>
    </div>
  );
}
