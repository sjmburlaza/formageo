import { test, expect } from '@playwright/test';

test('shows the project workspace entry point', async ({ page }) => {
  await page.goto('/projects');

  await expect(page.getByRole('heading', { name: 'Projects' })).toBeVisible();
  await expect(
    page.getByRole('button', { name: 'Create project' }),
  ).toBeVisible();
});
