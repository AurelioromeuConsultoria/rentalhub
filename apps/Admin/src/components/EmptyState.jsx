import { Link } from 'react-router-dom';

export function EmptyState({ icon, title, description, actions = [], compact = false }) {
  return (
    <div className={`inline-empty empty-state${compact ? ' compact' : ''}`}>
      <span className="empty-state-icon">{icon}</span>
      <strong>{title}</strong>
      {description && <span>{description}</span>}
      {actions.length > 0 && (
        <div className="empty-state-actions">
          {actions.map((action) => {
            const className = action.variant === 'secondary' ? 'secondary-action' : 'primary-action';

            if (action.to) {
              return (
                <Link className={className} key={action.label} to={action.to}>
                  {action.icon}
                  {action.label}
                </Link>
              );
            }

            return (
              <button className={className} key={action.label} type="button" onClick={action.onClick}>
                {action.icon}
                {action.label}
              </button>
            );
          })}
        </div>
      )}
    </div>
  );
}
