// Рендер HTML-страниц (консоли и схемы) в PNG для отчёта.
const { chromium } = require('/opt/node22/lib/node_modules/playwright');
const path = require('path'), fs = require('fs');
(async () => {
  const [shotsDir, diagHtml, outDir] = process.argv.slice(2);
  const b = await chromium.launch(); const p = await b.newPage({ deviceScaleFactor: 2, viewport: { width: 1400, height: 1000 } });
  for (const f of fs.readdirSync(shotsDir).filter(f => f.endsWith('.html'))) {
    await p.goto('file://' + path.join(shotsDir, f));
    await p.locator('#shot').screenshot({ path: path.join(outDir, f.replace('.html', '.png')) });
  }
  await p.goto('file://' + diagHtml);
  await p.waitForFunction(() => document.querySelectorAll('svg').length >= 2, null, { timeout: 30000 });
  await p.waitForTimeout(800);
  for (const id of ['classes', 'algo']) await p.locator('#' + id).screenshot({ path: path.join(outDir, `schema_${id}.png`) });
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
