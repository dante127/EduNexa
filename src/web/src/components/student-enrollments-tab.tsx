"use client";

import { useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { Enrollment } from "@/lib/students-types";
import type { AcademicYear, ClassSection, GradeLevel } from "@/lib/structure-types";
import {
  Button,
  EmptyState,
  ErrorBanner,
  Modal,
  SelectField,
  StatusBadge,
  TextField,
  type StatusTone
} from "@/components/ui";

function enrollmentTone(status: string): StatusTone {
  if (status === "Enrolled") return "active";
  if (status === "Graduated") return "active";
  if (status === "Completed") return "closed";
  return "muted";
}

const today = () => new Date().toISOString().slice(0, 10);

export default function StudentEnrollmentsTab({
  studentId,
  enrollments,
  years,
  grades,
  sections,
  onChanged
}: {
  studentId: string;
  enrollments: Enrollment[];
  years: AcademicYear[];
  grades: GradeLevel[];
  sections: ClassSection[];
  onChanged: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [createOpen, setCreateOpen] = useState(false);
  const [changeSection, setChangeSection] = useState<Enrollment | null>(null);
  const [closing, setClosing] = useState<{ enrollment: Enrollment; action: "transfer" | "withdraw" | "graduate" } | null>(null);

  const yearName = (id: string) => years.find((year) => year.id === id)?.name ?? id;
  const gradeName = (id: string) => {
    const grade = grades.find((level) => level.id === id);
    return grade ? grade.nameEn : id;
  };
  const sectionName = (id: string) => sections.find((section) => section.id === id)?.name ?? id;
  const sectionsFor = (yearId: string, gradeId: string) =>
    sections.filter((section) => section.academicYearId === yearId && section.gradeLevelId === gradeId);

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{t("enrollments.title")}</h3>
        <Button onClick={() => setCreateOpen(true)}>{t("enrollments.create")}</Button>
      </div>

      {enrollments.length === 0 ? (
        <EmptyState message={t("enrollments.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("enrollments.year")}</th>
                <th className="px-4 py-3 text-start">{t("enrollments.grade")}</th>
                <th className="px-4 py-3 text-start">{t("enrollments.section")}</th>
                <th className="px-4 py-3 text-start">{t("enrollments.status")}</th>
                <th className="px-4 py-3 text-start">{t("enrollments.startDate")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {enrollments.map((enrollment) => (
                <tr key={enrollment.id}>
                  <td className="px-4 py-3 text-slate-600">{yearName(enrollment.academicYearId)}</td>
                  <td className="px-4 py-3 text-slate-600">{gradeName(enrollment.gradeLevelId)}</td>
                  <td className="px-4 py-3 text-slate-600">{sectionName(enrollment.classSectionId)}</td>
                  <td className="px-4 py-3">
                    <StatusBadge
                      tone={enrollmentTone(enrollment.status)}
                      label={t(`enrollments.statuses.${enrollment.status}`)}
                    />
                  </td>
                  <td className="px-4 py-3 text-slate-600">
                    {enrollment.startDate}
                    {enrollment.endDate ? ` — ${enrollment.endDate}` : ""}
                  </td>
                  <td className="px-4 py-3">
                    {enrollment.status === "Enrolled" ? (
                      <div className="flex flex-wrap justify-end gap-2">
                        <Button variant="secondary" onClick={() => setChangeSection(enrollment)}>
                          {t("enrollments.changeSection")}
                        </Button>
                        <Button variant="secondary" onClick={() => setClosing({ enrollment, action: "transfer" })}>
                          {t("enrollments.transfer")}
                        </Button>
                        <Button variant="secondary" onClick={() => setClosing({ enrollment, action: "withdraw" })}>
                          {t("enrollments.withdraw")}
                        </Button>
                        <Button variant="secondary" onClick={() => setClosing({ enrollment, action: "graduate" })}>
                          {t("enrollments.graduate")}
                        </Button>
                      </div>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <CreateEnrollmentModal
        open={createOpen}
        studentId={studentId}
        years={years}
        grades={grades}
        sectionsFor={sectionsFor}
        onClose={() => setCreateOpen(false)}
        onSaved={async () => {
          setCreateOpen(false);
          await onChanged();
        }}
      />

      {changeSection ? (
        <ChangeSectionModal
          open
          enrollment={changeSection}
          sections={sectionsFor(changeSection.academicYearId, changeSection.gradeLevelId).filter(
            (section) => section.id !== changeSection.classSectionId
          )}
          onClose={() => setChangeSection(null)}
          onSaved={async () => {
            setChangeSection(null);
            await onChanged();
          }}
        />
      ) : null}

      {closing ? (
        <CloseEnrollmentModal
          open
          enrollment={closing.enrollment}
          action={closing.action}
          onClose={() => setClosing(null)}
          onSaved={async () => {
            setClosing(null);
            await onChanged();
          }}
        />
      ) : null}
    </div>
  );
}

function CreateEnrollmentModal({
  open,
  studentId,
  years,
  grades,
  sectionsFor,
  onClose,
  onSaved
}: {
  open: boolean;
  studentId: string;
  years: AcademicYear[];
  grades: GradeLevel[];
  sectionsFor: (yearId: string, gradeId: string) => ClassSection[];
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [yearId, setYearId] = useState("");
  const [gradeId, setGradeId] = useState("");
  const [sectionId, setSectionId] = useState("");
  const [startDate, setStartDate] = useState(today());
  const [notes, setNotes] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (open) {
      setYearId("");
      setGradeId("");
      setSectionId("");
      setStartDate(today());
      setNotes("");
      setServerError(null);
    }
  }, [open]);

  const availableSections = yearId && gradeId ? sectionsFor(yearId, gradeId) : [];

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/students/${studentId}/enrollments`, {
        method: "POST",
        body: JSON.stringify({
          academicYearId: yearId,
          gradeLevelId: gradeId,
          classSectionId: sectionId,
          startDate,
          notes: notes.trim() === "" ? null : notes.trim()
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("enrollments.createTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <SelectField
          label={t("enrollments.year")}
          value={yearId}
          onChange={(event) => {
            setYearId(event.target.value);
            setSectionId("");
          }}
        >
          <option value="">{t("common.select")}</option>
          {years.map((year) => (
            <option key={year.id} value={year.id}>
              {year.name}
            </option>
          ))}
        </SelectField>
        <SelectField
          label={t("enrollments.grade")}
          value={gradeId}
          onChange={(event) => {
            setGradeId(event.target.value);
            setSectionId("");
          }}
        >
          <option value="">{t("common.select")}</option>
          {grades.map((grade) => (
            <option key={grade.id} value={grade.id}>
              {grade.nameEn}
            </option>
          ))}
        </SelectField>
        <SelectField label={t("enrollments.section")} value={sectionId} onChange={(event) => setSectionId(event.target.value)}>
          <option value="">{t("common.select")}</option>
          {availableSections.map((section) => (
            <option key={section.id} value={section.id}>
              {section.name}
            </option>
          ))}
        </SelectField>
        <TextField
          type="date"
          label={t("enrollments.startDate")}
          value={startDate}
          onChange={(event) => setStartDate(event.target.value)}
        />
        <TextField label={t("enrollments.notes")} value={notes} onChange={(event) => setNotes(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving || !yearId || !gradeId || !sectionId || !startDate}>
            {saving ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function ChangeSectionModal({
  open,
  enrollment,
  sections,
  onClose,
  onSaved
}: {
  open: boolean;
  enrollment: Enrollment;
  sections: ClassSection[];
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [sectionId, setSectionId] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/enrollments/${enrollment.id}/change-section`, {
        method: "POST",
        body: JSON.stringify({ classSectionId: sectionId })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("enrollments.changeSectionTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <SelectField
          label={t("enrollments.newSection")}
          value={sectionId}
          onChange={(event) => setSectionId(event.target.value)}
        >
          <option value="">{t("common.select")}</option>
          {sections.map((section) => (
            <option key={section.id} value={section.id}>
              {section.name}
            </option>
          ))}
        </SelectField>
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving || !sectionId}>
            {saving ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function CloseEnrollmentModal({
  open,
  enrollment,
  action,
  onClose,
  onSaved
}: {
  open: boolean;
  enrollment: Enrollment;
  action: "transfer" | "withdraw" | "graduate";
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [endDate, setEndDate] = useState(today());
  const [notes, setNotes] = useState("");
  const [serverError, setServerError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const confirmMessage =
    action === "transfer"
      ? t("enrollments.confirmTransfer")
      : action === "withdraw"
        ? t("enrollments.confirmWithdraw")
        : t("enrollments.confirmGraduate");

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSaving(true);
    setServerError(null);
    try {
      await apiJson(`/api/enrollments/${enrollment.id}/${action}`, {
        method: "POST",
        body: JSON.stringify({
          endDate,
          notes: notes.trim() === "" ? null : notes.trim()
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal open={open} title={t("enrollments.closeTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <p className="text-sm text-slate-600">{confirmMessage}</p>
        <TextField
          type="date"
          label={t("enrollments.endDateLabel")}
          value={endDate}
          onChange={(event) => setEndDate(event.target.value)}
        />
        <TextField label={t("enrollments.notes")} value={notes} onChange={(event) => setNotes(event.target.value)} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={saving || !endDate}>
            {saving ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
