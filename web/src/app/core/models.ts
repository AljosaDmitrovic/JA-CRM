// API contracts. These mirror the C# records in src/JaCrm.Api/Features/*/Contracts.cs;
// change both sides together. Dates: timestamps are ISO-8601 UTC strings, date-only values are
// 'yyyy-MM-dd' strings.

export type ApplicationStatus =
  | 'Interested'
  | 'Researching'
  | 'ReadyToApply'
  | 'Applied'
  | 'RecruiterInterview'
  | 'TechnicalInterview'
  | 'HiringManagerInterview'
  | 'FinalInterview'
  | 'OfferReceived'
  | 'Rejected'
  | 'Withdrawn'
  | 'NoResponse';
export type ApplicationPhase = 'Pipeline' | 'Applied' | 'Interviewing' | 'Offer' | 'Closed';
export type Priority = 'Low' | 'Medium' | 'High';
export type CompanySize = 'From1To10' | 'From11To50' | 'From51To200' | 'From201To1000' | 'From1001To5000' | 'Over5000';
export type Seniority = 'Intern' | 'Junior' | 'Mid' | 'Senior' | 'Lead' | 'Principal';
export type VacancySource = 'LinkedIn' | 'CompanyWebsite' | 'Referral' | 'Recruiter' | 'JobBoard' | 'Other';
export type WorkArrangement = 'Remote' | 'Hybrid' | 'OnSite';
export type TriState = 'Unknown' | 'Yes' | 'No';
export type EmploymentType = 'FullTime' | 'PartTime' | 'Contractor' | 'Freelance' | 'Internship';
export type PayPeriod = 'Hourly' | 'Monthly' | 'Annual';
export type SalaryType = 'Gross' | 'Net' | 'Unknown';
export type Comparability = 'Comparable' | 'Net' | 'UnknownType' | 'MissingRate';
export type ThemePreference = 'System' | 'Light' | 'Dark';
export type DateFormatPreference = 'DayMonthYear' | 'Iso';
export type ArchivedFilter = 'Active' | 'Archived' | 'All';

export interface Option<T extends string = string> {
  value: T;
  label: string;
}

export interface Meta {
  statuses: { value: ApplicationStatus; label: string; phase: ApplicationPhase }[];
  phases: { value: ApplicationPhase; label: string; statuses: ApplicationStatus[] }[];
  priorities: Option<Priority>[];
  companySizes: Option<CompanySize>[];
  seniorities: Option<Seniority>[];
  sources: Option<VacancySource>[];
  workArrangements: Option<WorkArrangement>[];
  triStates: Option<TriState>[];
  employmentTypes: Option<EmploymentType>[];
  payPeriods: Option<PayPeriod>[];
  salaryTypes: Option<SalaryType>[];
  themes: Option<ThemePreference>[];
  dateFormats: Option<DateFormatPreference>[];
  commonCurrencies: string[];
}

export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// Salaries ---------------------------------------------------------------------------------------

export interface SalaryValue {
  amount: number;
  currency: string;
  period: PayPeriod;
  type: SalaryType;
}

export interface SalaryRange {
  min: number | null;
  max: number | null;
  currency: string;
  period: PayPeriod;
  type: SalaryType;
}

/** Annual amount(s) in the base currency. For a single amount, min === max. */
export interface NormalizedSalary {
  annualMin: number | null;
  annualMax: number | null;
  baseCurrency: string;
  comparability: Comparability;
}

// Companies --------------------------------------------------------------------------------------

export interface CompanyListItem {
  id: number;
  name: string;
  website: string | null;
  industry: string | null;
  country: string | null;
  city: string | null;
  size: CompanySize | null;
  rating: number | null;
  isArchived: boolean;
  createdAt: string;
  vacancyCount: number;
  applicationCount: number;
  activeApplicationCount: number;
}

export interface CompanyOption {
  id: number;
  name: string;
  isArchived: boolean;
}

export interface CompanyRequest {
  name: string;
  website: string | null;
  careersUrl: string | null;
  industry: string | null;
  headquarters: string | null;
  country: string | null;
  city: string | null;
  size: CompanySize | null;
  notes: string | null;
  rating: number | null;
}

export interface CompanyDetail extends CompanyRequest {
  id: number;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  vacancies: VacancySummary[];
  applications: ApplicationSummary[];
  history: StatusHistoryEntry[];
}

export interface DuplicateCompany {
  id: number;
  name: string;
  website: string | null;
  isArchived: boolean;
  reason: 'Name' | 'Domain';
}

// Vacancies --------------------------------------------------------------------------------------

export interface VacancySummary {
  id: number;
  title: string;
  seniority: Seniority | null;
  workArrangement: WorkArrangement | null;
  country: string | null;
  city: string | null;
  dateDiscovered: string | null;
  applicationDeadline: string | null;
  isArchived: boolean;
  applicationId: number | null;
  applicationStatus: ApplicationStatus | null;
}

export interface VacancyListItem extends VacancySummary {
  companyId: number;
  companyName: string;
  employmentType: EmploymentType | null;
  openToMyLocation: TriState;
  relocationSupport: TriState;
  requiredTechnologies: string[];
  postedSalary: SalaryRange | null;
  normalizedSalary: NormalizedSalary | null;
  createdAt: string;
}

export interface NewCompanyInline {
  name: string;
}

export interface VacancyRequest {
  title: string;
  companyId: number | null;
  newCompany: NewCompanyInline | null;
  seniority: Seniority | null;
  description: string | null;
  url: string | null;
  source: VacancySource | null;
  sourceDetail: string | null;
  country: string | null;
  city: string | null;
  workArrangement: WorkArrangement | null;
  remoteEligibility: string | null;
  openToMyLocation: TriState;
  relocationSupport: TriState;
  relocationNotes: string | null;
  employmentType: EmploymentType | null;
  requiredTechnologies: string[];
  preferredTechnologies: string[];
  requiredExperienceYears: number | null;
  requiredExperienceText: string | null;
  postedSalary: SalaryRange | null;
  dateDiscovered: string | null;
  applicationDeadline: string | null;
  notes: string | null;
}

export interface VacancyDetail extends Omit<VacancyRequest, 'newCompany' | 'companyId'> {
  id: number;
  companyId: number;
  companyName: string;
  companyIsArchived: boolean;
  normalizedSalary: NormalizedSalary | null;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  applicationId: number | null;
  applicationStatus: ApplicationStatus | null;
}

// Applications -----------------------------------------------------------------------------------

export interface ApplicationSummary {
  id: number;
  vacancyId: number;
  title: string;
  status: ApplicationStatus;
  priority: Priority;
  isArchived: boolean;
  submittedOn: string | null;
  updatedAt: string;
}

export interface ApplicationListItem {
  id: number;
  vacancyId: number;
  companyId: number;
  companyName: string;
  title: string;
  status: ApplicationStatus;
  phase: ApplicationPhase;
  priority: Priority;
  isArchived: boolean;
  isStale: boolean;
  createdAt: string;
  updatedAt: string;
  statusChangedAt: string;
  submittedOn: string | null;
  country: string | null;
  city: string | null;
  workArrangement: WorkArrangement | null;
  seniority: Seniority | null;
  employmentType: EmploymentType | null;
  /** Offered salary if set, otherwise the posted range from the snapshot (decision D-26). */
  salarySource: 'Offered' | 'Posted' | null;
  salary: SalaryRange | null;
  normalizedSalary: NormalizedSalary | null;
}

export interface PostingSnapshot {
  jobTitle: string;
  companyName: string;
  seniority: Seniority | null;
  description: string | null;
  requiredTechnologies: string[];
  preferredTechnologies: string[];
  requiredExperienceYears: number | null;
  requiredExperienceText: string | null;
  country: string | null;
  city: string | null;
  workArrangement: WorkArrangement | null;
  remoteEligibility: string | null;
  openToMyLocation: TriState;
  relocationSupport: TriState;
  relocationNotes: string | null;
  employmentType: EmploymentType | null;
  postedSalary: SalaryRange | null;
  url: string | null;
  capturedAt: string;
}

export interface StatusHistoryEntry {
  id: number;
  applicationId: number;
  jobTitle: string;
  fromStatus: ApplicationStatus | null;
  toStatus: ApplicationStatus;
  changedAt: string;
  note: string | null;
}

export interface ApplicationUpdateRequest {
  priority: Priority;
  submittedOn: string | null;
  notes: string | null;
  reasonForApplying: string | null;
  expectedSalary: SalaryValue | null;
  offeredSalary: SalaryValue | null;
  compensationNotes: string | null;
  cvVersion: string | null;
  coverLetter: string | null;
}

export interface ApplicationDetail extends ApplicationUpdateRequest {
  id: number;
  vacancyId: number;
  vacancyTitle: string;
  vacancyIsArchived: boolean;
  companyId: number;
  companyName: string;
  status: ApplicationStatus;
  phase: ApplicationPhase;
  isArchived: boolean;
  archivedAt: string | null;
  isStale: boolean;
  createdAt: string;
  updatedAt: string;
  statusChangedAt: string;
  expectedNormalized: NormalizedSalary | null;
  offeredNormalized: NormalizedSalary | null;
  postedNormalized: NormalizedSalary | null;
  snapshot: PostingSnapshot;
  history: StatusHistoryEntry[];
}

export interface CreateApplicationRequest {
  vacancyId: number | null;
  newVacancy: { title: string; companyId: number | null; newCompany: NewCompanyInline | null; url: string | null } | null;
  status: ApplicationStatus;
  priority: Priority;
}

// Dashboard, search, settings --------------------------------------------------------------------

export interface Dashboard {
  metrics: {
    totalApplications: number;
    activeApplications: number;
    interviewsInProgress: number;
    offersReceived: number;
    rejectedApplications: number;
    awaitingResponse: number;
    staleApplications: number;
  };
  staleThresholdDays: number;
  pipeline: { phase: ApplicationPhase; count: number; statuses: { status: ApplicationStatus; count: number }[] }[];
  stale: { id: number; title: string; companyName: string; statusChangedAt: string; daysSinceChange: number }[];
  recentActivity: {
    kind: 'Created' | 'StatusChanged' | 'Updated';
    at: string;
    applicationId: number;
    title: string;
    companyName: string;
    fromStatus: ApplicationStatus | null;
    toStatus: ApplicationStatus | null;
  }[];
  upcomingDeadlines: {
    vacancyId: number;
    title: string;
    companyName: string;
    deadline: string;
    daysLeft: number;
    applicationId: number | null;
    applicationStatus: ApplicationStatus | null;
  }[];
}

export interface SearchResults {
  applications: { id: number; title: string; companyName: string; status: ApplicationStatus; isArchived: boolean }[];
  vacancies: { id: number; title: string; companyName: string; isArchived: boolean }[];
  companies: { id: number; name: string; country: string | null; isArchived: boolean }[];
}

export interface ExchangeRate {
  currency: string;
  rateToBase: number;
  updatedOn: string;
}

export interface Settings {
  theme: ThemePreference;
  baseCurrency: string;
  workingHoursPerMonth: number;
  staleThresholdDays: number;
  dateFormat: DateFormatPreference;
  exchangeRates: ExchangeRate[];
}

export interface Lookups {
  countries: string[];
  cities: string[];
  industries: string[];
  technologies: string[];
}

export interface ImportSummary {
  companies: number;
  vacancies: number;
  applications: number;
  statusHistory: number;
  technologies: number;
  exchangeRates: number;
  backupFile: string;
}

export interface CreatedId {
  id: number;
}

/** Query parameters shared by list endpoints. Undefined/null/empty values are not sent. */
export type QueryParams = Record<string, string | number | boolean | readonly string[] | null | undefined>;
