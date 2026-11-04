const { chromium } = require('/opt/node22/lib/node_modules/playwright');
(async () => {
  const [html, out] = process.argv.slice(2);
  const b = await chromium.launch(); const p = await b.newPage({ deviceScaleFactor: 2, viewport: { width: 1600, height: 1200 } });
  await p.goto('file://' + html); await p.waitForFunction(() => document.querySelectorAll('svg').length >= 3, null, { timeout: 30000 });
  await p.waitForTimeout(800);
  for (const id of ['er', 'algo', 'net']) await p.locator('#' + id).screenshot({ path: `${out}/${id}.png` });
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
