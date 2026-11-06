function IconPencil() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M12 20h9" />
      <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z" />
    </svg>
  )
}

function IconTrash() {
  return (
    <svg viewBox="0 0 24 24" aria-hidden="true">
      <path d="M3 6h18" />
      <path d="M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6" />
      <path d="M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
      <line x1="10" y1="11" x2="10" y2="17" />
      <line x1="14" y1="11" x2="14" y2="17" />
    </svg>
  )
}

function RowActionButton({ tone, icon, verb, entity, name, onClick }) {
  const tooltip = `${verb} ${entity}`
  return (
    <button
      className={`icon-button icon-button-${tone}`}
      type="button"
      onClick={onClick}
      aria-label={name ? `${tooltip}: ${name}` : tooltip}
      data-tooltip={tooltip}
    >
      {icon}
    </button>
  )
}

export function EditButton({ entity, name, onClick }) {
  return <RowActionButton tone="neutral" icon={<IconPencil />} verb="Edit" entity={entity} name={name} onClick={onClick} />
}

export function DeleteButton({ entity, name, onClick }) {
  return <RowActionButton tone="danger" icon={<IconTrash />} verb="Delete" entity={entity} name={name} onClick={onClick} />
}
