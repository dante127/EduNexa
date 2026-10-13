export type AcademicStatus = "Planned" | "Active" | "Closed";

export interface AcademicYear {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: AcademicStatus;
}

export interface AcademicTerm {
  id: string;
  academicYearId: string;
  name: string;
  sequence: number;
  startDate: string;
  endDate: string;
  status: AcademicStatus;
}

export interface GradeLevel {
  id: string;
  sequence: number;
  nameEn: string;
  nameAr: string;
}

export interface Subject {
  id: string;
  code: string;
  nameEn: string;
  nameAr: string;
  isActive: boolean;
}

export interface Classroom {
  id: string;
  name: string;
  capacity: number;
  isActive: boolean;
}

export interface ClassSection {
  id: string;
  academicYearId: string;
  gradeLevelId: string;
  name: string;
  capacity: number;
}

export interface SchoolSettings {
  id: string;
  nameEn: string;
  nameAr: string;
  timezone: string;
  defaultLocale: string;
  currencyCode: string;
  address?: string | null;
  logoPath?: string | null;
  phone?: string | null;
  email?: string | null;
}
