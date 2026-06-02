const currencyFormatter = new Intl.NumberFormat('pt-BR', {
  style: 'currency',
  currency: 'BRL',
});

function formatCurrency(value) {
  return currencyFormatter.format(Number(value || 0));
}

function parseCurrencyInput(value) {
  const rawValue = String(value || '').trim();
  const digits = rawValue.replace(/\D/g, '');
  if (!digits) {
    return '';
  }

  if (!rawValue.includes('R$')) {
    if (rawValue.includes(',') || rawValue.includes('.')) {
      const normalized = rawValue
        .replace(/[^\d,.]/g, '')
        .replace(/\./g, '')
        .replace(',', '.');
      return (Number(normalized) || 0).toFixed(2);
    }

    return Number(digits).toFixed(2);
  }

  return (Number(digits) / 100).toFixed(2);
}

export function MoneyField({ label, value, onChange, required, span = false }) {
  return (
    <label className={`form-field${span ? ' span-2' : ''}`}>
      <span>{label}</span>
      <input
        type="text"
        inputMode="numeric"
        value={value === '' || value === null || value === undefined ? '' : formatCurrency(value)}
        onChange={(event) => onChange(parseCurrencyInput(event.target.value))}
        onFocus={(event) => event.target.select()}
        placeholder="R$ 0,00"
        required={required}
      />
    </label>
  );
}
