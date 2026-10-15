"use client";

import { useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { Guardian, StudentGuardianLink } from "@/lib/students-types";
import {
  Button,
  CheckboxField,
  ConfirmDialog,
  EmptyState,
  ErrorBanner,
  Modal,
  SelectField,
  TextField
} from "@/components/ui";

const relationships = ["Father", "Mother", "Guardian", "Other"] as const;

export default function StudentGuardiansTab({
  studentId,
  guardians,
  onChanged
}: {
  studentId: string;
  guardians: StudentGuardianLink[];
  onChanged: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [createOpen, setCreateOpen] = useState(false);
  const [linkOpen, setLinkOpen] = useState(false);
  const [editing, setEditing] = useState<StudentGuardianLink | null>(null);
  const [unlinking, setUnlinking] = useState<StudentGuardianLink | null>(null);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const makePrimary = async (link: StudentGuardianLink) => {
    setBusyId(link.guardianId);
    setActionError(null);
    try {
      await apiJson(`/api/students/${studentId}/guardians/${link.guardianId}`, {
        method: "PUT",
        body: JSON.stringify({
          relationship: link.relationship,
          isPrimary: true,
          isEmergencyContact: link.isEmergencyContact,
          isAuthorizedPickup: link.isAuthorizedPickup,
          custodyNotes: link.custodyNotes ?? null
        })
      });
      await onChanged();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusyId(null);
    }
  };

  const confirmUnlink = async () => {
    if (!unlinking) {
      return;
    }

    setBusyId(unlinking.guardianId);
    setActionError(null);
    try {
      await apiJson(`/api/students/${studentId}/guardians/${unlinking.guardianId}`, { method: "DELETE" });
      await onChanged();
    } catch (error) {
      setActionError(formatApiError(t, error));
    } finally {
      setBusyId(null);
      setUnlinking(null);
    }
  };

  return (
    <div>
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 className="text-sm font-semibold uppercase tracking-wide text-slate-500">{t("guardians.title")}</h3>
        <div className="flex gap-2">
          <Button variant="secondary" onClick={() => setLinkOpen(true)}>
            {t("guardians.link")}
          </Button>
          <Button onClick={() => setCreateOpen(true)}>{t("guardians.add")}</Button>
        </div>
      </div>

      {actionError ? (
        <div className="mb-3">
          <ErrorBanner message={actionError} />
        </div>
      ) : null}

      {guardians.length === 0 ? (
        <EmptyState message={t("guardians.empty")} />
      ) : (
        <div className="overflow-hidden rounded-xl border border-slate-200 bg-white">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 text-start">{t("guardians.fullNameEn")}</th>
                <th className="px-4 py-3 text-start">{t("guardians.relationship")}</th>
                <th className="px-4 py-3 text-start">{t("guardians.phone")}</th>
                <th className="px-4 py-3 text-end">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {guardians.map((link) => (
                <tr key={link.guardianId}>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap items-center gap-2 font-medium">
                      {link.fullNameEn}
                      {link.isPrimary ? (
                        <span className="rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-medium text-emerald-800">
                          {t("guardians.primary")}
                        </span>
                      ) : null}
                    </div>
                    <div className="text-xs text-slate-500">{link.fullNameAr}</div>
                    <div className="mt-1 flex flex-wrap gap-1 text-xs text-slate-500">
                      {link.isEmergencyContact ? (
                        <span className="rounded bg-slate-100 px-1.5 py-0.5">{t("guardians.isEmergency")}</span>
                      ) : null}
                      {link.isAuthorizedPickup ? (
                        <span className="rounded bg-slate-100 px-1.5 py-0.5">{t("guardians.isPickup")}</span>
                      ) : null}
                    </div>
                  </td>
                  <td className="px-4 py-3 text-slate-600">{t(`guardians.relationships.${link.relationship}`)}</td>
                  <td className="px-4 py-3 text-slate-600">{link.phone ?? "—"}</td>
                  <td className="px-4 py-3">
                    <div className="flex flex-wrap justify-end gap-2">
                      {!link.isPrimary ? (
                        <Button
                          variant="secondary"
                          disabled={busyId === link.guardianId}
                          onClick={() => void makePrimary(link)}
                        >
                          {t("guardians.makePrimary")}
                        </Button>
                      ) : null}
                      <Button variant="secondary" onClick={() => setEditing(link)}>
                        {t("common.edit")}
                      </Button>
                      <Button variant="danger" onClick={() => setUnlinking(link)}>
                        {t("guardians.unlink")}
                      </Button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <CreateGuardianModal
        open={createOpen}
        studentId={studentId}
        onClose={() => setCreateOpen(false)}
        onSaved={async () => {
          setCreateOpen(false);
          await onChanged();
        }}
      />
      <LinkGuardianModal
        open={linkOpen}
        studentId={studentId}
        linkedIds={guardians.map((link) => link.guardianId)}
        onClose={() => setLinkOpen(false)}
        onSaved={async () => {
          setLinkOpen(false);
          await onChanged();
        }}
      />
      {editing ? (
        <EditGuardianLinkModal
          open
          studentId={studentId}
          link={editing}
          onClose={() => setEditing(null)}
          onSaved={async () => {
            setEditing(null);
            await onChanged();
          }}
        />
      ) : null}
      <ConfirmDialog
        open={unlinking !== null}
        title={t("guardians.unlinkTitle")}
        message={t("guardians.unlinkConfirm")}
        confirmLabel={t("guardians.unlink")}
        cancelLabel={t("common.cancel")}
        danger
        onConfirm={() => void confirmUnlink()}
        onCancel={() => setUnlinking(null)}
      />
    </div>
  );
}

const guardianSchema = z.object({
  fullNameEn: z.string().min(1).max(150),
  fullNameAr: z.string().min(1).max(150),
  phone: z.string().max(30),
  altPhone: z.string().max(30),
  email: z.union([z.literal(""), z.string().email().max(200)]),
  occupation: z.string().max(150),
  address: z.string().max(300)
});

type GuardianFormValues = z.infer<typeof guardianSchema>;

const linkFieldsSchema = z.object({
  relationship: z.string().min(1),
  isPrimary: z.boolean(),
  isEmergencyContact: z.boolean(),
  isAuthorizedPickup: z.boolean(),
  custodyNotes: z.string().max(500)
});

type LinkFormValues = z.infer<typeof linkFieldsSchema>;

function CreateGuardianModal({
  open,
  studentId,
  onClose,
  onSaved
}: {
  open: boolean;
  studentId: string;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [serverError, setServerError] = useState<string | null>(null);
  const guardianForm = useForm<GuardianFormValues>({
    resolver: zodResolver(guardianSchema),
    defaultValues: {
      fullNameEn: "",
      fullNameAr: "",
      phone: "",
      altPhone: "",
      email: "",
      occupation: "",
      address: ""
    }
  });
  const linkForm = useForm<LinkFormValues>({
    resolver: zodResolver(linkFieldsSchema),
    defaultValues: {
      relationship: "Father",
      isPrimary: false,
      isEmergencyContact: false,
      isAuthorizedPickup: true,
      custodyNotes: ""
    }
  });
  const { errors } = guardianForm.formState;

  useEffect(() => {
    if (open) {
      guardianForm.reset();
      linkForm.reset();
      setServerError(null);
    }
  }, [open, guardianForm, linkForm]);

  const emptyToNull = (value: string) => (value.trim() === "" ? null : value.trim());

  const submit = guardianForm.handleSubmit(async (guardianValues) => {
    setServerError(null);
    try {
      const guardian = await apiJson<Guardian>("/api/guardians", {
        method: "POST",
        body: JSON.stringify({
          fullNameEn: guardianValues.fullNameEn.trim(),
          fullNameAr: guardianValues.fullNameAr.trim(),
          phone: emptyToNull(guardianValues.phone),
          altPhone: emptyToNull(guardianValues.altPhone),
          email: emptyToNull(guardianValues.email),
          occupation: emptyToNull(guardianValues.occupation),
          address: emptyToNull(guardianValues.address)
        })
      });

      const link = linkForm.getValues();
      await apiJson(`/api/students/${studentId}/guardians`, {
        method: "POST",
        body: JSON.stringify({
          guardianId: guardian.id,
          relationship: link.relationship,
          isPrimary: link.isPrimary,
          isEmergencyContact: link.isEmergencyContact,
          isAuthorizedPickup: link.isAuthorizedPickup,
          custodyNotes: emptyToNull(link.custodyNotes)
        })
      });

      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  });

  return (
    <Modal open={open} title={t("guardians.createTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <div className="grid grid-cols-2 gap-3">
          <TextField
            label={t("guardians.fullNameEn")}
            error={errors.fullNameEn ? t("common.required") : undefined}
            {...guardianForm.register("fullNameEn")}
          />
          <TextField
            label={t("guardians.fullNameAr")}
            error={errors.fullNameAr ? t("common.required") : undefined}
            {...guardianForm.register("fullNameAr")}
          />
          <TextField label={t("guardians.phone")} {...guardianForm.register("phone")} />
          <TextField label={t("guardians.altPhone")} {...guardianForm.register("altPhone")} />
          <TextField label={t("guardians.email")} {...guardianForm.register("email")} />
          <TextField label={t("guardians.occupation")} {...guardianForm.register("occupation")} />
        </div>
        <TextField label={t("guardians.address")} {...guardianForm.register("address")} />

        <div className="border-t border-slate-200 pt-4">
          <SelectField label={t("guardians.relationship")} {...linkForm.register("relationship")}>
            {relationships.map((relationship) => (
              <option key={relationship} value={relationship}>
                {t(`guardians.relationships.${relationship}`)}
              </option>
            ))}
          </SelectField>
          <div className="mt-3 space-y-2">
            <CheckboxField label={t("guardians.isPrimary")} {...linkForm.register("isPrimary")} />
            <CheckboxField label={t("guardians.isEmergency")} {...linkForm.register("isEmergencyContact")} />
            <CheckboxField label={t("guardians.isPickup")} {...linkForm.register("isAuthorizedPickup")} />
          </div>
          <div className="mt-3">
            <TextField label={t("guardians.custodyNotes")} {...linkForm.register("custodyNotes")} />
          </div>
        </div>

        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={guardianForm.formState.isSubmitting}>
            {guardianForm.formState.isSubmitting ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function LinkGuardianModal({
  open,
  studentId,
  linkedIds,
  onClose,
  onSaved
}: {
  open: boolean;
  studentId: string;
  linkedIds: string[];
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [searchInput, setSearchInput] = useState("");
  const [results, setResults] = useState<Guardian[] | null>(null);
  const [selected, setSelected] = useState<Guardian | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const linkForm = useForm<LinkFormValues>({
    resolver: zodResolver(linkFieldsSchema),
    defaultValues: {
      relationship: "Father",
      isPrimary: false,
      isEmergencyContact: false,
      isAuthorizedPickup: true,
      custodyNotes: ""
    }
  });

  const search = async (term: string) => {
    setServerError(null);
    try {
      const query = term.trim() ? `?search=${encodeURIComponent(term.trim())}` : "";
      setResults(await apiJson<Guardian[]>(`/api/guardians${query}`));
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  useEffect(() => {
    if (open) {
      setSearchInput("");
      setSelected(null);
      setResults(null);
      setServerError(null);
      linkForm.reset();
      void search("");
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]);

  const submit = linkForm.handleSubmit(async (values) => {
    if (!selected) {
      setServerError(t("guardians.selectGuardian"));
      return;
    }

    setServerError(null);
    try {
      await apiJson(`/api/students/${studentId}/guardians`, {
        method: "POST",
        body: JSON.stringify({
          guardianId: selected.id,
          relationship: values.relationship,
          isPrimary: values.isPrimary,
          isEmergencyContact: values.isEmergencyContact,
          isAuthorizedPickup: values.isAuthorizedPickup,
          custodyNotes: values.custodyNotes.trim() === "" ? null : values.custodyNotes.trim()
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  });

  const available = (results ?? []).filter((guardian) => !linkedIds.includes(guardian.id));

  return (
    <Modal open={open} title={t("guardians.link")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <div className="flex items-end gap-2">
          <div className="flex-1">
            <TextField
              label={t("guardians.searchLabel")}
              placeholder={t("guardians.searchPlaceholder")}
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
            />
          </div>
          <Button type="button" variant="secondary" onClick={() => void search(searchInput)}>
            {t("common.search")}
          </Button>
        </div>

        <div className="max-h-48 overflow-y-auto rounded-lg border border-slate-200">
          {results === null ? (
            <div className="px-3 py-4 text-center text-sm text-slate-500">{t("common.loading")}</div>
          ) : available.length === 0 ? (
            <div className="px-3 py-4 text-center text-sm text-slate-500">{t("guardians.noResults")}</div>
          ) : (
            <ul className="divide-y divide-slate-100">
              {available.map((guardian) => (
                <li key={guardian.id}>
                  <button
                    type="button"
                    onClick={() => setSelected(guardian)}
                    className={`block w-full px-3 py-2 text-start text-sm hover:bg-slate-50 ${
                      selected?.id === guardian.id ? "bg-slate-100 font-medium" : ""
                    }`}
                  >
                    <div>
                      {guardian.fullNameEn} / {guardian.fullNameAr}
                    </div>
                    <div className="text-xs text-slate-500">{guardian.phone ?? "—"}</div>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        <SelectField label={t("guardians.relationship")} {...linkForm.register("relationship")}>
          {relationships.map((relationship) => (
            <option key={relationship} value={relationship}>
              {t(`guardians.relationships.${relationship}`)}
            </option>
          ))}
        </SelectField>
        <div className="space-y-2">
          <CheckboxField label={t("guardians.isPrimary")} {...linkForm.register("isPrimary")} />
          <CheckboxField label={t("guardians.isEmergency")} {...linkForm.register("isEmergencyContact")} />
          <CheckboxField label={t("guardians.isPickup")} {...linkForm.register("isAuthorizedPickup")} />
        </div>
        <TextField label={t("guardians.custodyNotes")} {...linkForm.register("custodyNotes")} />

        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={linkForm.formState.isSubmitting || !selected}>
            {linkForm.formState.isSubmitting ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}

function EditGuardianLinkModal({
  open,
  studentId,
  link,
  onClose,
  onSaved
}: {
  open: boolean;
  studentId: string;
  link: StudentGuardianLink;
  onClose: () => void;
  onSaved: () => void | Promise<void>;
}) {
  const t = useTranslations("students");
  const [serverError, setServerError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { isSubmitting }
  } = useForm<LinkFormValues>({
    resolver: zodResolver(linkFieldsSchema),
    defaultValues: {
      relationship: link.relationship,
      isPrimary: link.isPrimary,
      isEmergencyContact: link.isEmergencyContact,
      isAuthorizedPickup: link.isAuthorizedPickup,
      custodyNotes: link.custodyNotes ?? ""
    }
  });

  const submit = handleSubmit(async (values) => {
    setServerError(null);
    try {
      await apiJson(`/api/students/${studentId}/guardians/${link.guardianId}`, {
        method: "PUT",
        body: JSON.stringify({
          relationship: values.relationship,
          isPrimary: values.isPrimary,
          isEmergencyContact: values.isEmergencyContact,
          isAuthorizedPickup: values.isAuthorizedPickup,
          custodyNotes: values.custodyNotes.trim() === "" ? null : values.custodyNotes.trim()
        })
      });
      await onSaved();
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  });

  return (
    <Modal open={open} title={t("guardians.editTitle")} onClose={onClose}>
      <form className="space-y-4" onSubmit={submit} noValidate>
        <p className="text-sm text-slate-600">
          {link.fullNameEn} / {link.fullNameAr}
        </p>
        <SelectField label={t("guardians.relationship")} {...register("relationship")}>
          {relationships.map((relationship) => (
            <option key={relationship} value={relationship}>
              {t(`guardians.relationships.${relationship}`)}
            </option>
          ))}
        </SelectField>
        <div className="space-y-2">
          <CheckboxField label={t("guardians.isPrimary")} {...register("isPrimary")} />
          <CheckboxField label={t("guardians.isEmergency")} {...register("isEmergencyContact")} />
          <CheckboxField label={t("guardians.isPickup")} {...register("isAuthorizedPickup")} />
        </div>
        <TextField label={t("guardians.custodyNotes")} {...register("custodyNotes")} />
        {serverError ? <ErrorBanner message={serverError} /> : null}
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" disabled={isSubmitting}>
            {isSubmitting ? t("common.saving") : t("common.save")}
          </Button>
        </div>
      </form>
    </Modal>
  );
}
