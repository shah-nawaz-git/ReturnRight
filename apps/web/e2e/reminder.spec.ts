import { expect, test } from "@playwright/test";

import { apiPost, registerOn } from "./helpers";

const MAILPIT_URL = process.env.MAILPIT_URL || "http://localhost:8025";

// API-level test — run once on the desktop project.
test.skip(({ isMobile }) => Boolean(isMobile), "API-level, desktop only");

interface MailpitMessage {
  ID: string;
  Subject: string;
  To: { Address: string }[];
}

test("due follow-up sends an email reminder with a link to the case", async ({
  request,
}) => {
  test.setTimeout(120_000);
  const { email } = await registerOn(request);

  const caseRes = await apiPost(request, "/api/cases", {
    problemType: "DamagedItem",
    description: "The item arrived broken and I want a refund for it.",
    requestedOutcomeType: "FullRefund",
    requestedAmount: 10,
    requestedCurrency: "EUR",
  });
  expect(caseRes.status()).toBe(201);
  const { id: caseId } = (await caseRes.json()) as { id: string };

  // Due almost immediately — the dev worker polls every ~5s.
  const due = new Date(Date.now() + 2_000);
  const fuRes = await apiPost(request, `/api/cases/${caseId}/follow-ups`, {
    title: "Check for seller reply",
    dueAt: due.toISOString(),
  });
  expect(fuRes.status(), await fuRes.text()).toBe(201);

  // Poll reminders until the email reminder is sent.
  let sent: { channel: string; status: string } | undefined;
  await expect
    .poll(
      async () => {
        const res = await request.get(`/api/cases/${caseId}/reminders`);
        const reminders = (await res.json()) as {
          channel: string;
          status: string;
        }[];
        sent = reminders.find((r) => r.channel === "Email" && r.status === "Sent");
        return sent ? "sent" : reminders.map((r) => r.status).join(",");
      },
      { timeout: 45_000, intervals: [1_000, 2_000, 3_000] },
    )
    .toBe("sent");
  expect(sent).toBeTruthy();

  // Mailpit received it with the case link.
  await expect
    .poll(
      async () => {
        const res = await request.get(
          `${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`,
        );
        if (!res.ok()) return `http ${res.status()}`;
        const body = (await res.json()) as {
          total: number;
          messages: MailpitMessage[];
        };
        return body.total >= 1 ? "found" : "none";
      },
      { timeout: 15_000, intervals: [1_000, 2_000] },
    )
    .toBe("found");

  const search = await request.get(
    `${MAILPIT_URL}/api/v1/search?query=${encodeURIComponent(`to:${email}`)}`,
  );
  const { messages } = (await search.json()) as { messages: MailpitMessage[] };
  const latest = messages[0];
  expect(latest.Subject).toBe("Follow-up due for your ReturnRight case");

  const msg = await request.get(`${MAILPIT_URL}/api/v1/message/${latest.ID}`);
  const detail = (await msg.json()) as { Text?: string; HTML?: string };
  const body = `${detail.Text ?? ""}${detail.HTML ?? ""}`;
  expect(body).toContain(`/app/cases/${caseId}`);
  expect(body).toContain("Check for seller reply");
});
