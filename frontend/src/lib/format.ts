export const labels = {
  Active: "Actif",
  Archived: "Archivé",
  Draft: "Brouillon",
  Expired: "Expiré",
  Terminated: "Rompu",
  Pending: "En attente",
  Approved: "Acceptée",
  Rejected: "Refusée",
  Present: "Présent",
  Late: "Retard",
  Absent: "Absent",
  Published: "Publiée",
  Closed: "Clôturée",
  Received: "Reçue",
  InReview: "En étude",
  Interview: "Entretien",
  Accepted: "Acceptée",
  Open: "Ouvert",
  InProgress: "En cours",
  Done: "Terminée",
  Backlog: "Backlog",
  Cancelled: "Annulé",
  Administrateur: "Administrateur",
  ResponsableRH: "Responsable RH",
  Manager: "Manager",
  Employe: "Employé",
  Morning: "Matin",
  Afternoon: "Après-midi",
} as const;

export function label(value: string) {
  return labels[value as keyof typeof labels] ?? value;
}

export function formatDate(value?: string | null) {
  if (!value) return "—";
  const [year, month, day] = value.slice(0, 10).split("-");
  return day ? `${day}/${month}/${year}` : value;
}

export function formatTime(value?: string | null) {
  return value ? value.slice(0, 5) : "—";
}

export function fullName(first: string, last: string) {
  return `${first} ${last}`;
}
