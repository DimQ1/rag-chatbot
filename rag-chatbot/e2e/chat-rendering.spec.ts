import { expect, test } from '@playwright/test';

test.describe('Chat Markdown and diagram rendering', () => {
  test.beforeEach(async ({ page }) => {
    const now = new Date().toISOString();
    const sessionId = '11111111-1111-1111-1111-111111111111';

    await page.addInitScript(() => {
      localStorage.setItem(
        'auth_token',
        JSON.stringify({
          id: 'user-1',
          email: 'rag-admin@example.com',
          name: 'RAG Admin',
          token: 'mock-jwt-token',
          role: 'Admin',
        }),
      );
    });

    await page.route('**/api/chatsession', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: sessionId,
            topic: 'Markdown render verification',
            isPinned: false,
            createdAtUtc: now,
            updatedAtUtc: now,
            messageCount: 2,
          },
        ]),
      });
    });

    await page.route(`**/api/chatsession/${sessionId}`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: sessionId,
          topic: 'Markdown render verification',
          isPinned: false,
          createdAtUtc: now,
          updatedAtUtc: now,
          messageCount: 2,
          messages: [
            {
              id: 'user-msg-1',
              role: 'user',
              content: 'Show markdown and a flow chart',
              sources: [],
              createdAtUtc: now,
            },
            {
              id: 'assistant-msg-1',
              role: 'assistant',
              content:
                '## Pig Brothers\n\nOrdered list:\n1. Percy\n2. Peter\n3. Patrick\n\n```mermaid\nflowchart TD\n  A[Ask] --> B[Search]\n  B --> C[Answer]\n```',
              sources: [
                {
                  title: 'The Three Little Pigs',
                  url: 'local://knowledge/the-three-little-pigs.md',
                },
              ],
              createdAtUtc: now,
            },
          ],
        }),
      });
    });
  });

  test('renders Markdown headings and ordered lists in assistant response', async ({ page }) => {
    await page.goto('/chat');

    const assistantBubble = page.locator('.message.assistant .bubble').first();

    await expect(assistantBubble.getByRole('heading', { name: 'Pig Brothers' })).toBeVisible();
    await expect(assistantBubble.locator('ol > li')).toHaveCount(3);
    await expect(assistantBubble.locator('ol > li').nth(0)).toContainText('Percy');
  });

  test('renders Mermaid diagram SVG in assistant response', async ({ page }) => {
    await page.goto('/chat');

    const diagram = page.locator('.message.assistant .mermaid-diagram svg').first();

    await expect(diagram).toBeVisible();
    await expect(diagram.locator('g, path, text').first()).toBeVisible();
  });
});
