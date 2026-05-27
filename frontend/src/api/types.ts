export const CATEGORIES = [
  "Noise",
  "Smell",
  "SmokingOrIncense",
  "Parking",
  "SafetyConcern",
  "CommonAreaMisuse",
  "Other",
] as const;
export type Category = (typeof CATEGORIES)[number];

export const CATEGORY_LABELS: Record<Category, string> = {
  Noise: "Noise",
  Smell: "Smell",
  SmokingOrIncense: "Smoking / Incense",
  Parking: "Parking",
  SafetyConcern: "Safety concern",
  CommonAreaMisuse: "Common area misuse",
  Other: "Other",
};

// Short label + emoji for the big tappable chips in quick capture.
export const CATEGORY_META: Record<Category, { emoji: string; short: string }> = {
  Noise: { emoji: "🔊", short: "Noise" },
  Smell: { emoji: "👃", short: "Smell" },
  SmokingOrIncense: { emoji: "🚬", short: "Smoke" },
  Parking: { emoji: "🅿️", short: "Parking" },
  SafetyConcern: { emoji: "⚠️", short: "Safety" },
  CommonAreaMisuse: { emoji: "🧹", short: "Shared space" },
  Other: { emoji: "📝", short: "Other" },
};

export const LANGUAGES = [
  { code: "en", label: "English" },
  { code: "fi", label: "Suomi" },
  { code: "sv", label: "Svenska" },
  { code: "ar", label: "العربية" },
  { code: "ru", label: "Русский" },
  { code: "et", label: "Eesti" },
  { code: "so", label: "Soomaali" },
] as const;

export interface EntryListItem {
  id: string;
  category: Category;
  occurredAt: string;
  reporterName: string;
  subjectApartment: string | null;
  snippet: string;
  hasAttachment: boolean;
  edited: boolean;
  archived: boolean;
}

export interface TranslationDto {
  targetLanguage: string;
  translatedText: string;
  provider: string;
  status: "pending" | "completed" | "failed";
  isMachineGenerated: boolean;
}

export interface RevisionDto {
  id: string;
  previousText: string;
  editedAt: string;
}

export interface EntryDetail {
  id: string;
  category: Category;
  occurredAt: string;
  reporterName: string;
  reporterUserId: string;
  subjectApartment: string | null;
  originalText: string;
  originalLanguage: string;
  sharedLanguage: string;
  translations: TranslationDto[];
  attachmentIds: string[];
  createdAt: string;
  editedAt: string | null;
  archived: boolean;
  revisions: RevisionDto[];
}

export interface BuildingDto {
  id: string;
  name: string;
  address: string | null;
  sharedLanguage: string;
  role: "resident" | "board" | "admin";
  joinCode: string | null;
}

export interface MemberDto {
  userId: string;
  displayName: string;
  role: string;
  apartmentNumber: string | null;
}
