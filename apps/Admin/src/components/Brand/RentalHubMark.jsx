export function RentalHubMark({ title = 'RentalHub', className = '' }) {
  return (
    <svg
      aria-label={title}
      className={className}
      role="img"
      viewBox="0 0 106 106"
      xmlns="http://www.w3.org/2000/svg"
    >
      <rect width="106" height="106" rx="28" fill="#111D31" stroke="#315C7E" strokeWidth="2" />
      <rect x="11" y="11" width="84" height="84" rx="21" fill="#0B1220" fillOpacity="0.5" />
      <path
        d="M29 78V29H51C62.046 29 71 37.954 71 49C71 60.046 62.046 69 51 69H29"
        stroke="#F8FAFC"
        strokeWidth="8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
      <path d="M54 68L72 78" stroke="#55D6C5" strokeWidth="8" strokeLinecap="round" />
      <path d="M79 29V78" stroke="#F8FAFC" strokeWidth="8" strokeLinecap="round" />
      <path d="M63 53H79" stroke="#8EC7FF" strokeWidth="8" strokeLinecap="round" />
    </svg>
  );
}
