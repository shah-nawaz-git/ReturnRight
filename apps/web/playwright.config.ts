import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  use: {
    baseURL: process.env.PW_BASE_URL || "http://localhost:3000",
  },
  projects: [
    {
      name: "desktop",
      use: {
        ...devices["Desktop Chrome"],
        channel: process.env.PW_CHANNEL || undefined,
        viewport: { width: 1440, height: 900 },
      },
    },
    {
      name: "mobile",
      use: {
        ...devices["Pixel 7"],
        channel: process.env.PW_CHANNEL || undefined,
        viewport: { width: 390, height: 844 },
      },
    },
  ],
});
