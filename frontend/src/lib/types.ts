export type Role = "Administrateur" | "ResponsableRH" | "Manager" | "Employe" | string;

export type UserProfile = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  isActive: boolean;
  roles: Role[];
  employeeId: string | null;
};

export type SalaryAdvance = {
  id: string;
  amount: number;
  reason: string | null;
  status: string;
  createdAt: string;
};

export type WeeklyRating = {
  id: string;
  weekStart: string;
  score: number;
  comment: string | null;
  managerName: string;
};

export type LeaveAccrual = {
  accountCreatedOn: string;
  months: number;
  accruedDays: number;
  reservedDays: number;
  remainingDays: number;
};

export type WeekHours = { weekStart: string; hours: number };

export type Mission = {
  id: string;
  title: string;
  detail: string | null;
  status: string;
  comment: string | null;
  weekStart: string;
  managerName: string;
  createdAt: string;
};

export type TeamMember = { id: string; fullName: string; positionTitle: string };
export type TeamFollowUp = { missions: Mission[]; ratings: WeeklyRating[] };

export type EmployeeSpace = {
  positionNumber: string;
  professionalEmail: string;
  bankRib: string;
  baseSalary: number;
  bonus: number;
  salaryPaid: boolean;
  salaryYear: number;
  salaryMonth: number;
  hoursThisMonth: number;
  leaveBalances: LeaveBalance[];
  leaveRequests: LeaveRequest[];
  advances: SalaryAdvance[];
  ratings: WeeklyRating[];
  chipNumber: string | null;
  chipCredit: number | null;
  leave: LeaveAccrual;
  weeks: WeekHours[];
  missions: Mission[];
  salaryCut: { unpaidDays: number; dailyRate: number; amount: number };
};

export type Account = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: string;
  isActive: boolean;
  isApproved: boolean;
  createdAt: string;
};

export type LoginResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: UserProfile;
  requiresTwoFactor?: boolean;
};

export type AccountSettings = {
  firstName: string;
  lastName: string;
  email: string;
  twoFactorEnabled: boolean;
  hasPhoto: boolean;
};

export type TwoFactorSetup = { sharedKey: string; qrCodeDataUrl: string };

export type Paged<T> = { items: T[]; total: number; page: number; pageSize: number };

export type Department = { id: string; name: string; description: string | null; employeeCount: number };
export type Position = { id: string; title: string; description: string | null; departmentId: string; departmentName: string };
export type EmployeeSummary = { id: string; fullName: string; internalCode: string; managerId: string | null; departmentId: string; positionTitle: string };

export type Employee = {
  id: string;
  internalCode: string;
  firstName: string;
  lastName: string;
  professionalEmail: string;
  phone: string | null;
  hireDate: string;
  contractType: string;
  status: "Active" | "Archived";
  departmentId: string;
  departmentName: string;
  positionId: string;
  positionTitle: string;
  managerId: string | null;
  managerName: string | null;
  userId: string | null;
  archivedAt: string | null;
};

export type EmploymentEvent = { id: string; occurredAt: string; kind: string; description: string };
export type Contract = {
  id: string;
  employeeId: string;
  employeeName: string;
  type: string;
  status: "Draft" | "Active" | "Expired" | "Terminated";
  startDate: string;
  endDate: string | null;
  notes: string | null;
  expiringSoon: boolean;
};
export type DocumentItem = {
  id: string;
  employeeId: string;
  employeeName: string;
  contractId: string | null;
  fileName: string;
  contentType: string;
  uploadedAt: string;
};
export type LeaveBalance = {
  id: string;
  leaveTypeId: string;
  leaveTypeName: string;
  year: number;
  allocatedDays: number;
  usedDays: number;
  pendingDays: number;
  remainingDays: number;
};
export type EmployeeDetail = {
  profile: Employee;
  history: EmploymentEvent[];
  contracts: Contract[];
  documents: DocumentItem[];
  balances: LeaveBalance[];
  directReports: EmployeeSummary[];
};

export type LeaveType = { id: string; name: string; annualAllowance: number };
export type LeaveRequest = {
  id: string;
  employeeId: string;
  employeeName: string;
  leaveTypeId: string;
  leaveTypeName: string;
  startDate: string;
  endDate: string;
  businessDays: number;
  reason: string | null;
  status: "Pending" | "Approved" | "Rejected";
  decisionComment: string | null;
  decidedAt: string | null;
  createdAt: string;
  isHalfDay?: boolean;
  halfDayPart?: string | null;
  unpaidDays?: number;
};

export type Attendance = {
  id: string;
  employeeId: string;
  employeeName: string;
  departmentId: string;
  departmentName: string;
  date: string;
  checkIn: string | null;
  checkOut: string | null;
  status: "Present" | "Late" | "Absent";
  note: string | null;
};
export type AttendanceReport = {
  present: number;
  late: number;
  absent: number;
  byDepartment: { departmentId: string; departmentName: string; present: number; late: number; absent: number }[];
};

export type Dashboard = {
  employeeCount: number;
  pendingLeaveCount: number;
  expiringContractCount: number;
  byDepartment: { departmentId: string; name: string; count: number }[];
  pendingLeaves: LeaveRequest[];
  expiringContracts: Contract[];
  attendance: AttendanceReport;
  applicationsPerOffer: { offerId: string; title: string; candidateCount: number }[];
};

export type JobOffer = {
  id: string;
  title: string;
  description: string;
  requirements: string;
  positionId: string | null;
  positionTitle: string | null;
  status: "Draft" | "Published" | "Closed";
  createdAt: string;
  candidateCount: number;
};
export type Candidate = {
  id: string;
  jobOfferId: string;
  firstName: string;
  lastName: string;
  email: string;
  cvFileName: string | null;
  status: "Received" | "InReview" | "Interview" | "Accepted" | "Rejected";
  appliedAt: string;
};

export type Goal = {
  id: string;
  employeeId: string;
  employeeName: string;
  title: string;
  description: string | null;
  targetDate: string;
  progress: number;
  status: "Open" | "InProgress" | "Done" | "Cancelled";
};
export type Review = {
  id: string;
  employeeId: string;
  employeeName: string;
  reviewerEmployeeId: string;
  reviewerName: string;
  scheduledAt: string;
  comment: string | null;
  completedAt: string | null;
};

export type NotificationItem = { id: string; title: string; body: string; createdAt: string; readAt: string | null };
export type AuditItem = { id: number; occurredAt: string; actorUserId: string; action: string; entityName: string; entityId: string; details: string | null };
export type Organization = { departments: Department[]; positions: Position[]; employees: EmployeeSummary[] };
