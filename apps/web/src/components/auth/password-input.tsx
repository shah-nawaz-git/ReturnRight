"use client";

import { EyeIcon, EyeOffIcon } from "lucide-react";
import { forwardRef, useState, type ComponentProps } from "react";

import { cn } from "@/lib/utils";
import { Input } from "@/components/ui/input";

export const PasswordInput = forwardRef<HTMLInputElement, ComponentProps<"input">>(
  function PasswordInput({ className, ...props }, ref) {
    const [show, setShow] = useState(false);
    return (
      <div className="relative">
        <Input
          ref={ref}
          type={show ? "text" : "password"}
          className={cn("pr-10", className)}
          {...props}
        />
        <button
          type="button"
          aria-label={show ? "Hide password" : "Show password"}
          aria-pressed={show}
          onClick={() => setShow((s) => !s)}
          className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-muted-foreground hover:text-foreground"
        >
          {show ? <EyeOffIcon className="size-4" aria-hidden /> : <EyeIcon className="size-4" aria-hidden />}
        </button>
      </div>
    );
  },
);
