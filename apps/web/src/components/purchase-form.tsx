"use client";

import { PlusIcon, Trash2Icon } from "lucide-react";

import { currencyOptions } from "@/lib/labels";
import { cn } from "@/lib/utils";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";

export interface PurchaseItemValues {
  productName: string;
  quantity: string;
  unitPrice: string;
}

export interface PurchaseFormValues {
  merchantName: string;
  orderNumber: string;
  purchaseDate: string;
  currency: string;
  totalAmount: string;
  notes: string;
  items: PurchaseItemValues[];
}

export interface FieldMeta {
  confidence: number | null;
  edited: boolean;
}

export const emptyPurchaseValues: PurchaseFormValues = {
  merchantName: "",
  orderNumber: "",
  purchaseDate: "",
  currency: "EUR",
  totalAmount: "",
  notes: "",
  items: [{ productName: "", quantity: "1", unitPrice: "" }],
};

function sourceTag(meta: FieldMeta | undefined): "document" | "check" | null {
  if (!meta || meta.edited || meta.confidence === null) return null;
  return meta.confidence >= 0.75 ? "document" : "check";
}

export function validatePurchaseForm(values: PurchaseFormValues): Record<string, string> {
  const errors: Record<string, string> = {};
  if (!values.merchantName.trim()) errors.merchantName = "Add the shop or seller name.";
  if (!/^[A-Z]{3}$/.test(values.currency)) errors.currency = "Use a 3-letter code like EUR.";
  if (values.totalAmount.trim() !== "") {
    const total = Number(values.totalAmount);
    if (!Number.isFinite(total) || total < 0) errors.totalAmount = "Enter a valid amount.";
  }
  if (values.items.length === 0) {
    errors.items = "Add at least one item.";
  }
  values.items.forEach((item, i) => {
    if (!item.productName.trim()) errors[`item-${i}-name`] = "Name the item.";
    const qty = Number(item.quantity);
    if (!Number.isInteger(qty) || qty < 1) errors[`item-${i}-qty`] = "At least 1.";
    if (item.unitPrice.trim() !== "") {
      const price = Number(item.unitPrice);
      if (!Number.isFinite(price) || price < 0) errors[`item-${i}-price`] = "Enter a valid price.";
    }
  });
  return errors;
}

export function PurchaseForm({
  values,
  onChange,
  errors = {},
  meta = {},
  idPrefix = "pf",
}: {
  values: PurchaseFormValues;
  onChange: (values: PurchaseFormValues) => void;
  errors?: Record<string, string>;
  meta?: Partial<Record<"merchantName" | "orderNumber" | "purchaseDate" | "totalAmount", FieldMeta>>;
  idPrefix?: string;
}) {
  const set = <K extends keyof PurchaseFormValues>(key: K, value: PurchaseFormValues[K]) =>
    onChange({ ...values, [key]: value });

  const setItem = (index: number, patch: Partial<PurchaseItemValues>) => {
    const items = values.items.map((item, i) => (i === index ? { ...item, ...patch } : item));
    set("items", items);
  };

  const warningIfCheck = (key: "merchantName" | "orderNumber" | "purchaseDate" | "totalAmount") =>
    sourceTag(meta[key]) === "check" ? "border-warning" : undefined;

  return (
    <div className="space-y-4">
      <FormField
        label="Shop or seller"
        htmlFor={`${idPrefix}-merchant`}
        error={errors.merchantName}
        required
        sourceTag={sourceTag(meta.merchantName)}
      >
        <Input
          id={`${idPrefix}-merchant`}
          value={values.merchantName}
          onChange={(e) => set("merchantName", e.target.value)}
          className={warningIfCheck("merchantName")}
          autoComplete="off"
        />
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField
          label="Order or reference number"
          htmlFor={`${idPrefix}-order`}
          error={errors.orderNumber}
          sourceTag={sourceTag(meta.orderNumber)}
        >
          <Input
            id={`${idPrefix}-order`}
            value={values.orderNumber}
            onChange={(e) => set("orderNumber", e.target.value)}
            className={cn("font-mono", warningIfCheck("orderNumber"))}
            autoComplete="off"
          />
        </FormField>
        <FormField
          label="Purchase date"
          htmlFor={`${idPrefix}-date`}
          error={errors.purchaseDate}
          sourceTag={sourceTag(meta.purchaseDate)}
        >
          <Input
            id={`${idPrefix}-date`}
            type="date"
            value={values.purchaseDate}
            onChange={(e) => set("purchaseDate", e.target.value)}
            className={warningIfCheck("purchaseDate")}
          />
        </FormField>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label="Currency" htmlFor={`${idPrefix}-currency`} error={errors.currency} required>
          <Select value={values.currency} onValueChange={(v) => set("currency", String(v))}>
            <SelectTrigger id={`${idPrefix}-currency`} className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {currencyOptions.map((c) => (
                <SelectItem key={c} value={c}>
                  {c}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </FormField>
        <FormField
          label="Total paid"
          htmlFor={`${idPrefix}-total`}
          error={errors.totalAmount}
          sourceTag={sourceTag(meta.totalAmount)}
        >
          <Input
            id={`${idPrefix}-total`}
            inputMode="decimal"
            value={values.totalAmount}
            onChange={(e) => set("totalAmount", e.target.value)}
            className={warningIfCheck("totalAmount")}
            autoComplete="off"
          />
        </FormField>
      </div>

      <fieldset className="space-y-3">
        <legend className="text-sm font-medium">Items</legend>
        {errors.items ? (
          <p role="alert" className="text-xs text-destructive">{errors.items}</p>
        ) : null}
        {values.items.map((item, i) => (
          <div key={i} className="rounded-xl border border-border p-3">
            <div className="flex items-start gap-2">
              <div className="grid flex-1 gap-3 sm:grid-cols-[1fr_5rem_7rem]">
                <FormField
                  label={i === 0 ? "Item" : `Item ${i + 1}`}
                  htmlFor={`${idPrefix}-item-${i}-name`}
                  error={errors[`item-${i}-name`]}
                >
                  <Input
                    id={`${idPrefix}-item-${i}-name`}
                    value={item.productName}
                    onChange={(e) => setItem(i, { productName: e.target.value })}
                    autoComplete="off"
                  />
                </FormField>
                <FormField
                  label="Qty"
                  htmlFor={`${idPrefix}-item-${i}-qty`}
                  error={errors[`item-${i}-qty`]}
                >
                  <Input
                    id={`${idPrefix}-item-${i}-qty`}
                    inputMode="numeric"
                    value={item.quantity}
                    onChange={(e) => setItem(i, { quantity: e.target.value })}
                  />
                </FormField>
                <FormField
                  label="Unit price"
                  htmlFor={`${idPrefix}-item-${i}-price`}
                  error={errors[`item-${i}-price`]}
                >
                  <Input
                    id={`${idPrefix}-item-${i}-price`}
                    inputMode="decimal"
                    value={item.unitPrice}
                    onChange={(e) => setItem(i, { unitPrice: e.target.value })}
                  />
                </FormField>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="touch-target mt-6"
                aria-label={`Remove item ${i + 1}`}
                onClick={() => set("items", values.items.filter((_, j) => j !== i))}
                disabled={values.items.length <= 1}
              >
                <Trash2Icon aria-hidden />
              </Button>
            </div>
          </div>
        ))}
        <Button
          type="button"
          variant="outline"
          size="sm"
          className="touch-target"
          onClick={() =>
            set("items", [...values.items, { productName: "", quantity: "1", unitPrice: "" }])
          }
        >
          <PlusIcon aria-hidden /> Add item
        </Button>
      </fieldset>
    </div>
  );
}
