import { Construction } from 'lucide-react';

export function ModulePlaceholder({ title, description }) {
  return (
    <section className="placeholder-page">
      <div className="placeholder-icon">
        <Construction size={24} />
      </div>
      <span className="eyebrow">Em preparação</span>
      <h1>{title}</h1>
      <p>{description}</p>
    </section>
  );
}
