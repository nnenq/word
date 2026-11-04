// Проверка работы с SQL Server: основные сценарии и удаления, где важен обход каскадов
const { chromium } = require('/opt/node22/lib/node_modules/playwright');
const { execFileSync } = require('child_process');
const path = require('path');
const B = 'http://localhost:5091', OUT = process.argv[2];
const answersOf = l => JSON.parse(execFileSync('python3', ['-I', path.join(__dirname, 'answers_sql.py'), String(l)]).toString());
const ok = (c, m) => { console.log((c ? 'OK   ' : 'FAIL ') + m); if (!c) process.exitCode = 1; };
async function login(p, u, pw) { await p.goto(B + '/Account/Login'); await p.fill('#Login', u); await p.fill('#Password', pw); await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]); }
async function logout(p) { await Promise.all([p.waitForNavigation(), p.click('nav form button[type=submit]')]); }
async function confirm(p) { await p.waitForSelector('#confirmModal.show'); await Promise.all([p.waitForNavigation(), p.click('#confirmOk')]); }
async function fill(p, q, right) {
  const box = p.locator('.question').filter({ hasText: q.text }).first();
  const R = q.options.filter(o => o.correct), W = q.options.filter(o => !o.correct);
  if (q.type === 1 || q.type === 5) await box.locator('input.form-control').fill(right ? R[0].text : 'нет');
  else if (q.type === 2) await box.locator('label', { hasText: (right ? R[0] : W[0]).text }).first().click();
  else if (q.type === 3) for (const o of (right ? R : [W[0]])) await box.locator('label', { hasText: o.text }).first().click();
  else if (q.type === 4) for (let i = 0; i < q.options.length; i++) await box.locator('.match-row').filter({ hasText: q.options[i].text }).locator('select').selectOption({ label: right ? q.options[i].match : q.options[(i + 1) % q.options.length].match });
  else if (q.type === 6) await box.locator('textarea').fill('Развёрнутый ответ студента для проверки преподавателем.');
}
(async () => {
  const b = await chromium.launch(); const p = await b.newPage({ viewport: { width: 1366, height: 900 } });
  await login(p, 'orlov', 'Student123');
  ok((await p.request.get(B + '/Study/Lecture/2', { maxRedirects: 0 })).status() === 403, 'SQL Server: закрытая лекция → 403');
  const a = answersOf(1);
  await p.goto(B + '/Study/Lecture/1?tab=control'); for (const q of a.control) await fill(p, q, true);
  await Promise.all([p.waitForNavigation(), p.click('#control form button[type=submit]')]);
  await p.goto(B + '/Study/Lecture/1?tab=test'); await p.click('#test form button[type=submit]'); await confirm(p);
  for (const q of a.test) if (await p.locator('.question').filter({ hasText: q.text }).count()) await fill(p, q, true);
  await p.click('#testForm button[type=submit]'); await confirm(p);
  ok((await p.textContent('main')).includes('Тест сдан'), 'SQL Server: тест сдан, попытка сохранена');
  await logout(p);
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Reviews');
  const card = p.locator('.card').filter({ hasText: 'Орлов Дмитрий' }).first();
  await Promise.all([p.waitForNavigation(), card.locator('button[value=true]').click()]);
  ok((await p.textContent('.alert-success')).includes('открыта следующая'), 'SQL Server: ответ принят, следующая лекция открыта');
  for (const u of ['/Teacher', '/Teacher/Report?discipline=1', '/Teacher/Students', '/Teacher/Student/2', '/Teacher/Journal', '/Teacher/System', '/Teacher/Backup', '/Teacher/Lecture/1?tab=test'])
    ok((await p.goto(B + u)).status() === 200, 'SQL Server: страница ' + u);
  await p.goto(B + '/Teacher/System'); await p.screenshot({ path: OUT + '/sqlserver_system.png', fullPage: true });
  // Сброс результатов лекции (ExecuteDelete по связям)
  const sid = (await p.goto(B + '/Teacher/Students'), await p.locator('a', { hasText: 'Сидоров Павел' }).first().getAttribute('href'));
  await p.goto(B + sid);
  await p.locator('tr', { hasText: 'Основы тестирования' }).first().locator('form[action*="Reset"] button').click(); await confirm(p);
  ok((await p.textContent('.alert-success')).includes('сброшены'), 'SQL Server: сброс результатов лекции');
  // Удаление вопроса теста, лекции и дисциплины — варианты ответов удаляются кодом (обход каскадов)
  await p.goto(B + '/Teacher/Lecture/5?tab=test');
  await p.locator('#test form[action*="DeleteQuestion"] button').first().click(); await confirm(p);
  ok((await p.textContent('.alert-success')).includes('Вопрос удалён'), 'SQL Server: удаление вопроса теста');
  await p.goto(B + '/Teacher/Lecture/7');
  await p.locator('form[action*="handler=Delete"] button').first().click(); await confirm(p);
  ok((await p.textContent('.alert-success')).includes('удалена'), 'SQL Server: удаление лекции с тестом и попытками');
  await p.goto(B + '/Teacher/Disciplines');
  await p.locator('tr', { hasText: 'Основы алгоритмизации' }).locator('form button').click(); await confirm(p);
  ok((await p.textContent('.alert-success')).includes('удалена') && !(await p.textContent('tbody')).includes('Основы алгоритмизации'), 'SQL Server: удаление дисциплины со всеми результатами');
  await b.close();
})().catch(e => { console.error(e); process.exit(1); });
