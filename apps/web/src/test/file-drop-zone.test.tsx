import { describe, expect, it } from "vitest";

import { validateUpload } from "@/components/file-drop-zone";

function file(name: string, type: string, size = 1024) {
  return new File([new Uint8Array(size)], name, { type });
}

describe("validateUpload", () => {
  it("accepts pdf/jpg/png under 10 MB", () => {
    expect(validateUpload(file("receipt.pdf", "application/pdf"))).toBeNull();
    expect(validateUpload(file("photo.jpg", "image/jpeg"))).toBeNull();
    expect(validateUpload(file("photo.png", "image/png"))).toBeNull();
  });

  it("rejects unsupported types", () => {
    expect(validateUpload(file("notes.txt", "text/plain"))).toMatch(/PDF|JPG|PNG/i);
    expect(validateUpload(file("anim.gif", "image/gif"))).toMatch(/PDF|JPG|PNG/i);
  });

  it("rejects files over 10 MB", () => {
    const big = file("big.pdf", "application/pdf", 10 * 1024 * 1024 + 1);
    expect(validateUpload(big)).toMatch(/10\s*MB/i);
  });
});
