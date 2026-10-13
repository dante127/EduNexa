"use client";

import { useCallback, useEffect, useState } from "react";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { apiJson, formatApiError } from "@/lib/api-client";
import type { SchoolSettings } from "@/lib/structure-types";
import {
  Button,
  ErrorBanner,
  LoadingBlock,
  PageHeader,
  SelectField,
  TextField
} from "@/components/ui";

const settingsSchema = z.object({
  nameEn: z.string().min(1).max(200),
  nameAr: z.string().min(1).max(200),
  timezone: z.string().min(1).max(64),
  defaultLocale: z.string().min(1),
  currencyCode: z.string().length(3),
  address: z.string(),
  phone: z.string(),
  email: z.union([z.string().email(), z.literal("")])
});

type SettingsFormValues = z.infer<typeof settingsSchema>;

export default function SchoolSettingsPage() {
  const t = useTranslations("structure");
  const [state, setState] = useState<"loading" | "ready">("loading");
  const [loadError, setLoadError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting }
  } = useForm<SettingsFormValues>({
    resolver: zodResolver(settingsSchema)
  });

  const load = useCallback(async () => {
    setLoadError(null);
    try {
      const settings = await apiJson<SchoolSettings>("/api/school-settings");
      reset({
        nameEn: settings.nameEn,
        nameAr: settings.nameAr,
        timezone: settings.timezone,
        defaultLocale: settings.defaultLocale,
        currencyCode: settings.currencyCode,
        address: settings.address ?? "",
        phone: settings.phone ?? "",
        email: settings.email ?? ""
      });
      setState("ready");
    } catch (error) {
      setLoadError(formatApiError(t, error));
      setState("ready");
    }
  }, [reset, t]);

  useEffect(() => {
    void load();
  }, [load]);

  const onSubmit = async (values: SettingsFormValues) => {
    setServerError(null);
    setSaved(false);
    try {
      const settings = await apiJson<SchoolSettings>("/api/school-settings", {
        method: "PUT",
        body: JSON.stringify({
          ...values,
          address: values.address || null,
          phone: values.phone || null,
          email: values.email || null,
          logoPath: null
        })
      });
      reset({
        nameEn: settings.nameEn,
        nameAr: settings.nameAr,
        timezone: settings.timezone,
        defaultLocale: settings.defaultLocale,
        currencyCode: settings.currencyCode,
        address: settings.address ?? "",
        phone: settings.phone ?? "",
        email: settings.email ?? ""
      });
      setSaved(true);
    } catch (error) {
      setServerError(formatApiError(t, error));
    }
  };

  return (
    <div>
      <PageHeader title={t("settings.title")} description={t("settings.description")} />

      {loadError ? <ErrorBanner message={loadError} /> : null}

      {state === "loading" ? (
        <LoadingBlock message={t("common.loading")} />
      ) : (
        <form
          className="max-w-2xl space-y-4 rounded-xl border border-slate-200 bg-white p-6"
          onSubmit={handleSubmit(onSubmit)}
          noValidate
        >
          <div className="grid grid-cols-2 gap-3">
            <TextField
              label={t("settings.nameEn")}
              error={errors.nameEn ? t("common.required") : undefined}
              {...register("nameEn")}
            />
            <TextField
              label={t("settings.nameAr")}
              error={errors.nameAr ? t("common.required") : undefined}
              {...register("nameAr")}
            />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <TextField
              label={t("settings.timezone")}
              error={errors.timezone ? t("common.required") : undefined}
              {...register("timezone")}
            />
            <SelectField
              label={t("settings.defaultLocale")}
              error={errors.defaultLocale ? t("common.required") : undefined}
              {...register("defaultLocale")}
            >
              <option value="en">English</option>
              <option value="ar">العربية</option>
            </SelectField>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <TextField
              label={t("settings.currencyCode")}
              error={errors.currencyCode ? t("common.invalidCurrency") : undefined}
              {...register("currencyCode")}
            />
            <TextField
              label={t("settings.phone")}
              error={errors.phone ? t("common.invalidValue") : undefined}
              {...register("phone")}
            />
          </div>
          <TextField
            label={t("settings.email")}
            type="email"
            error={errors.email ? t("common.invalidEmail") : undefined}
            {...register("email")}
          />
          <TextField label={t("settings.address")} {...register("address")} />

          {serverError ? <ErrorBanner message={serverError} /> : null}
          {saved ? (
            <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-700">
              {t("common.saved")}
            </div>
          ) : null}

          <div className="flex justify-end">
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t("common.saving") : t("common.save")}
            </Button>
          </div>
        </form>
      )}
    </div>
  );
}
