export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface StudentSummary {
  id: string;
  studentNumber: string;
  firstNameEn: string;
  lastNameEn: string;
  firstNameAr: string;
  lastNameAr: string;
  dateOfBirth: string;
  gender: string;
  nationality?: string | null;
  status: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  photoPath?: string | null;
}

export interface StudentGuardianLink {
  guardianId: string;
  fullNameEn: string;
  fullNameAr: string;
  phone?: string | null;
  relationship: string;
  isPrimary: boolean;
  isEmergencyContact: boolean;
  isAuthorizedPickup: boolean;
  custodyNotes?: string | null;
}

export interface Enrollment {
  id: string;
  studentId: string;
  academicYearId: string;
  gradeLevelId: string;
  classSectionId: string;
  status: string;
  startDate: string;
  endDate?: string | null;
  notes?: string | null;
}

export interface StudentDetail {
  student: StudentSummary;
  guardians: StudentGuardianLink[];
  enrollments: Enrollment[];
}

export interface Guardian {
  id: string;
  fullNameEn: string;
  fullNameAr: string;
  phone?: string | null;
  altPhone?: string | null;
  email?: string | null;
  occupation?: string | null;
  address?: string | null;
}

export interface StudentDocument {
  id: string;
  documentType: string;
  fileName: string;
  mimeType: string;
  sizeBytes: number;
  verificationStatus: string;
  verifiedByUserId?: string | null;
  verifiedAt?: string | null;
  notes?: string | null;
  uploadedByUserId: string;
  uploadedAt: string;
}

export interface AdmissionApplication {
  id: string;
  applicantFirstNameEn: string;
  applicantLastNameEn: string;
  applicantFirstNameAr: string;
  applicantLastNameAr: string;
  dateOfBirth: string;
  gender: string;
  nationality?: string | null;
  academicYearId: string;
  desiredGradeLevelId: string;
  guardianName: string;
  guardianPhone?: string | null;
  guardianEmail?: string | null;
  status: string;
  submittedAt: string;
  reviewedByUserId?: string | null;
  reviewedAt?: string | null;
  decisionNotes?: string | null;
  convertedStudentId?: string | null;
}

export interface AdmissionEnrollmentResult {
  student: StudentSummary;
  enrollment: Enrollment;
  application: AdmissionApplication;
}
