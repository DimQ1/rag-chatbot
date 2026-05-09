---
name: embedded-browser-ui-testing
description: Test and debug Angular UI behavior in the VS Code embedded browser using DevTools MCP tools. Use for click tracking, session-panel behavior, routing checks, visual regressions, and DOM/state validation.
license: MIT
metadata:
  author: Workspace Local Skill
  version: '1.0'
---

# Embedded Browser UI Testing Skill

Use this skill when a task requires validating or debugging UI behavior in the embedded browser, especially with chat sessions, panel state, and interactive controls.

## When To Use

Use this skill for requests like:

- "check what I clicked"
- "validate UI in embedded browser"
- "inspect duplicate elements"
- "verify hide/open sidebar behavior"
- "ensure response appears in correct chat session"

## Required Workflow

1. Open or navigate the embedded page with `open_browser_page` or `navigate_page`.
2. Capture current structure with `read_page`.
3. If behavior is interaction-driven, reproduce steps with `click_element` and `type_in_page`.
4. For exact clicked target debugging, install a temporary click listener via `run_playwright_code` and read captured payload.
5. Validate resulting DOM/state using `read_page`.
6. If code changes are required, patch source files and run `get_errors` for changed files.

## Click Capture Snippet (Playwright)

Use this with `run_playwright_code` on the active page id:

```ts
await page.evaluate(() => {
  const key = '__uiClickCapture';
  const logKey = '__uiClickEvents';
  const w = window as any;

  if (!Array.isArray(w[logKey])) w[logKey] = [];
  if (w[key]) document.removeEventListener('click', w[key], true);

  const handler = (event: Event) => {
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;

    const node =
      target.closest('button,a,input,textarea,select,[role],[aria-label],[data-testid],div,span') ||
      target;

    w[logKey].push({
      ts: new Date().toISOString(),
      tag: node.tagName.toLowerCase(),
      id: node.id || null,
      classes: node.className || null,
      role: node.getAttribute('role'),
      ariaLabel: node.getAttribute('aria-label'),
      text: (node.textContent || '').trim().slice(0, 150),
    });

    if (w[logKey].length > 50) w[logKey].shift();
  };

  document.addEventListener('click', handler, true);
  w[key] = handler;
});
```

Read captured events:

```ts
return page.evaluate(() => {
  const w = window as any;
  return (w.__uiClickEvents || []).slice(-10);
});
```

## Validation Checklist

- No duplicate interactive controls for the same action.
- Active chat session highlight matches loaded conversation.
- Thinking indicator is scoped only to the session currently generating a response.
- Sidebar hide/open works and state is visually reflected.
- Sidebar resize handle (if present) updates width within limits.
- Document/source links navigate to DB-backed routes (e.g., `/document/{id}`), not legacy local links.

## Reporting Format

When reporting findings, include:

1. URL/page tested.
2. Action performed.
3. Observed result.
4. Expected result.
5. File(s) changed and how behavior changed.

## Safety And Scope

- Do not use destructive git commands.
- Keep edits minimal and targeted to the reproduced issue.
- Prefer fixing root cause in component/service code over CSS-only masking.
