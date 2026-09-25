"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";

import { problemTitle } from "@/lib/api/client";
import { useRegister } from "@/lib/api/hooks";
import { FormField } from "@/components/form-field";
import { PasswordInput } from "@/components/auth/password-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

const schema = z.object({
  email: z.string().email("Enter a valid email address"),
  password: z.string().min(10, "At least 10 characters"),
});

type Values = z.infer<typeof schema>;

function RegisterForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const registerMutation = useRegister();
  const [formError, setFormError] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<Values>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null);
    try {
      await registerMutation.mutateAsync(values);
      const next = searchParams.get("next");
      router.replace(next && next.startsWith("/") ? next : "/app");
    } catch (error) {
      setFormError(problemTitle(error, "Couldn't create your account. Try again."));
    }
  });

  return (
    <Card>
      <CardHeader>
        <h1 data-slot="card-title" className="font-heading text-lg leading-snug font-medium">
          Create your account
        </h1>
      </CardHeader>
      <CardContent>
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          {formError ? (
            <p role="alert" className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}
          <FormField label="Email" htmlFor="email" error={errors.email?.message}>
            <Input
              id="email"
              type="email"
              autoComplete="email"
              autoFocus
              {...register("email")}
            />
          </FormField>
          <FormField
            label="Password"
            htmlFor="password"
            error={errors.password?.message}
            hint="At least 10 characters"
          >
            <PasswordInput id="password" autoComplete="new-password" {...register("password")} />
          </FormField>
          <Button type="submit" className="touch-target w-full" size="lg" disabled={isSubmitting}>
            {isSubmitting ? "Creating…" : "Create account"}
          </Button>
        </form>
        <p className="mt-4 text-center text-sm text-muted-foreground">
          Already have an account?{" "}
          <Link href="/login" className="font-medium text-primary underline-offset-4 hover:underline">
            Log in
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}

export default function RegisterPage() {
  return (
    <Suspense>
      <RegisterForm />
    </Suspense>
  );
}
