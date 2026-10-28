# HRFlow — nomenclature

Carte du projet issue du cahier des charges. Le code métier se construit ensuite, une étape à la fois.

## Dépôts et rôles

| Dossier | Rôle |
| --- | --- |
| `backend/HrFlow.slnx` | Solution .NET 10 |
| `backend/HrFlow.Api` | ASP.NET Core Web API, C# |
| `backend/HrFlow.Api.Tests` | Tests xUnit |
| `frontend` | Next.js, App Router, TypeScript, Tailwind |
| `docker-compose.yml` | PostgreSQL 16 en développement |

Le navigateur parle à Next.js. Next.js parle à l’API en JSON. Seul l’API parle à PostgreSQL, via Entity Framework Core.

## Noms

- Code, classes, routes API : anglais (`Employee`, `/api/employees`).
- Textes vus par l’utilisateur : français.
- Rôles Identity : `Administrateur`, `ResponsableRH`, `Manager`, `Employe`.

## Découpage de l’API

```text
HrFlow.Api/
  Program.cs              hôte, services, pipeline HTTP
  Models/                 entités et énumérations
  Data/                   AppDbContext, migrations, jeu de démonstration
  Dtos/                   contrats JSON entrants et sortants
  Security/               rôles, règles pures, utilisateur courant
  Services/               cas d’usage
  Controllers/            endpoints REST
```

Une entité décrit une table. Un DTO décrit le JSON. Un service applique la règle métier. Un contrôleur ne fait que recevoir la requête et renvoyer le résultat.

## Modules du cahier des charges

| Étape | Module CDC | Entités | Routes API | Pages Next.js |
| --- | --- | --- | --- | --- |
| 1 | Socle technique | aucune table métier | `GET /api/health` | accueil : état de l’API et de PostgreSQL |
| 2 | Utilisateurs et rôles | `ApplicationUser` | `/api/auth`, `/api/users` | `/login`, `/mot-de-passe`, `/parametres` |
| 3 | Organisation | `Department`, `Position` | `/api/departments`, `/api/positions`, `/api/organization` | `/organisation` |
| 4 | Employés | `Employee`, `EmploymentEvent` | `/api/employees` | `/employes`, `/employes/[id]` |
| 5 | Contrats | `EmploymentContract` | `/api/contracts` | `/contrats` |
| 6 | Documents | `EmployeeDocument` | `/api/documents` | `/documents` |
| 7 | Congés | `LeaveType`, `LeaveBalance`, `LeaveRequest` | `/api/leave` | `/conges` |
| 8 | Présences | `AttendanceRecord` | `/api/attendance` | `/presences` |
| 9 | Tableau de bord | lectures seules | `/api/dashboard` | `/tableau-de-bord` |
| 10 | Notifications | `Notification`, `AuditEntry` | `/api/notifications`, `/api/audit` | cloche dans le cadre de page |
| 11 | Recrutement | `JobOffer`, `Candidate` | `/api/recruitment` | `/recrutement` |
| 12 | Performances | `PerformanceGoal`, `PerformanceReview` | `/api/performance` | `/evaluations` |
| 13 | Tests | — | — | — |
| 14 | Docker et CI | — | — | — |

Hors de cette série : intelligence artificielle, paie, application mobile.

## Qui voit quoi

- `Administrateur` : comptes, rôles, journal.
- `ResponsableRH` : employés, contrats, documents, congés, recrutement.
- `Manager` : son équipe, validation des congés et des présences, évaluations.
- `Employe` : son dossier, ses congés, ses documents, son pointage.

L’autorisation est décidée dans l’API, pas seulement masquée dans l’interface.
