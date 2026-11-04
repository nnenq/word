// Комплексная проверка «Электронного учебного кабинета»: все 12 проверок из раздела 7 задания и дополнительные.
// Сам запускает и перезапускает сайт, делает скриншоты для отчёта, сохраняет results.json.
const { chromium, devices } = require('/opt/node22/lib/node_modules/playwright');
const { spawn, execFileSync } = require('child_process');
const fs = require('fs'), path = require('path');
const [PROJECT, OUT] = process.argv.slice(2);
const B = 'http://localhost:5090';
const DB = path.join(PROJECT, 'App_Data/study.db');
fs.mkdirSync(OUT, { recursive: true });
const results = []; let shotN = 0;
function check(n, group, name, ok, detail = '') {
  results.push({ n, group, name, ok: !!ok, detail });
  console.log((ok ? 'OK   ' : 'FAIL ') + `[${n}] ${name}` + (detail ? ' — ' + detail : ''));
}
async function shot(p, name, full = true) { await p.waitForTimeout(250); if (full) await p.addStyleTag({ content: '.app-nav{position:static!important}' }); await p.screenshot({ path: `${OUT}/${String(++shotN).padStart(2, '0')}_${name}.png`, fullPage: full }); }
let server;
async function startServer() {
  server = spawn('dotnet', ['bin/Debug/net8.0/StudyCabinet.dll', '--urls', B], { cwd: PROJECT, env: { ...process.env, ASPNETCORE_ENVIRONMENT: 'Development' }, stdio: 'ignore' });
  for (let i = 0; i < 120; i++) { try { const r = await fetch(B + '/health'); if (r.ok) return; } catch { } await new Promise(r => setTimeout(r, 500)); }
  throw new Error('сайт не запустился');
}
async function stopServer() { server.kill('SIGTERM'); await new Promise(r => server.on('exit', r)); }
const answersOf = lecture => JSON.parse(execFileSync('python3', ['-I', path.join(__dirname, 'answers.py'), DB, String(lecture)]).toString());
const sql = q => execFileSync('python3', ['-I', '-c', `import sqlite3,sys;c=sqlite3.connect(sys.argv[1]);r=c.execute(sys.argv[2]).fetchall();c.commit();print(r)`, DB, q]).toString().trim();

async function login(p, user, pass) {
  await p.goto(B + '/Account/Login'); await p.fill('#Login', user); await p.fill('#Password', pass);
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
}
async function logout(p) { await Promise.all([p.waitForNavigation(), p.click('nav form button[type=submit]')]); }
async function confirmModal(p) { await p.waitForSelector('#confirmModal.show'); await Promise.all([p.waitForNavigation(), p.click('#confirmOk')]); }

/** Заполняет вопрос на странице: mode = right | wrong. */
async function fill(p, q, mode) {
  const box = p.locator('.question').filter({ hasText: q.text }).first();
  const right = q.options.filter(o => o.correct), wrong = q.options.filter(o => !o.correct);
  switch (q.type) {
    case 1: case 5: await box.locator('input.form-control').fill(mode === 'right' ? right[0].text : 'не знаю'); break;
    case 2: await box.locator('label', { hasText: (mode === 'right' ? right[0] : wrong[0]).text }).first().click(); break;
    case 3: for (const o of (mode === 'right' ? right : [wrong[0]])) await box.locator('label', { hasText: o.text }).first().click(); break;
    case 4: {
      const sels = box.locator('select');
      for (let i = 0; i < q.options.length; i++) {
        const left = q.options[i].text;
        const sel = box.locator('.match-row').filter({ hasText: left }).locator('select');
        await sel.selectOption({ label: mode === 'right' ? q.options[i].match : q.options[(i + 1) % q.options.length].match });
      }
      break;
    }
    case 6: await box.locator('textarea').fill('Проверить все комбинации входных данных невозможно: например, у двух полей с числами от 1 до 1000 уже миллион сочетаний. Поэтому выбирают важные и граничные случаи.'); break;
  }
}
/** Проходит тест лекции: rightCount — сколько вопросов ответить верно (остальные неверно). */
async function takeTest(p, lectureId, rightCount) {
  await p.goto(`${B}/Study/Lecture/${lectureId}?tab=test`);
  await p.click('#test form button[type=submit]'); await confirmModal(p);
  const qs = answersOf(lectureId).test;
  const shown = [];
  for (const q of qs) if (await p.locator('.question').filter({ hasText: q.text }).count()) shown.push(q);
  for (let i = 0; i < shown.length; i++) await fill(p, shown[i], i < rightCount ? 'right' : 'wrong');
  return shown;
}
async function submitTest(p) { await p.click('#testForm button[type=submit]'); await confirmModal(p); }

(async () => {
  await startServer();
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1366, height: 860 }, locale: 'ru-RU' });
  const p = await ctx.newPage();
  const jsErrors = []; p.on('pageerror', e => jsErrors.push(e.message));

  // ---------- Главная, регистрация ----------
  await p.goto(B + '/'); await shot(p, 'glavnaya');
  await p.goto(B + '/Account/Register');
  await p.fill('#Input_FullName', 'Ив@нов'); await p.fill('#Input_Login', 'ab'); await p.fill('#Input_Password', '123'); await p.fill('#Input_Confirm', '321');
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
  const regErr = (await p.locator('.text-danger').allTextContents()).join(' ');
  check('Д1', 'Регистрация', 'Проверка введённых данных при регистрации', regErr.includes('ФИО') && regErr.includes('Логин') && regErr.includes('Пароль') && regErr.includes('группу'));
  await shot(p, 'registraciya_oshibki');
  await p.fill('#Input_FullName', 'Новиков Артём'); await p.selectOption('#Input_GroupId', { label: 'ИС-22' });
  await p.fill('#Input_Login', 'novikov'); await p.fill('#Input_Password', 'Study2026'); await p.fill('#Input_Confirm', 'Study2026');
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
  check('Д2', 'Регистрация', 'Регистрация студента и вход в кабинет', p.url().endsWith('/Study') && (await p.textContent('main')).includes('Новиков Артём'));
  const hash = sql("select PasswordHash from Users where Login='novikov'");
  check('Д3', 'Безопасность', 'Пароль хранится в виде хеша, а не текстом', !hash.includes('Study2026') && hash.length > 60, hash.slice(2, 30) + '…');
  await logout(p);

  // ---------- 1. Авторизация преподавателя ----------
  await login(p, 'teacher', 'Teacher123');
  check(1, 'Авторизация', 'Авторизация преподавателя: открывается панель преподавателя', p.url().endsWith('/Teacher') && (await p.textContent('h1')).includes('Панель преподавателя'));
  await shot(p, 'panel_prepodavatelya');
  await p.goto(B + '/Teacher/Disciplines'); await shot(p, 'discipliny');
  await p.goto(B + '/Teacher/Discipline/1'); await shot(p, 'struktura_discipliny');
  await p.goto(B + '/Teacher/Lecture/1'); await shot(p, 'redaktor_lekcii');
  await p.goto(B + '/Teacher/Lecture/1?tab=control'); await shot(p, 'kontrolnye_voprosy_redaktor');
  await p.goto(B + '/Teacher/Lecture/1?tab=test'); await shot(p, 'test_nastroiki');
  await p.goto(B + '/Teacher/Question?lecture=1&kind=test&id=5'); await shot(p, 'redaktor_voprosa_sootvetstvie');

  // CRUD: новая дисциплина, тема, лекция, вопрос
  await p.goto(B + '/Teacher/Disciplines');
  await p.fill('#Title', 'Компьютерные сети'); await p.fill('#Description', 'Проверочная дисциплина автотеста');
  await Promise.all([p.waitForNavigation(), p.click('main .col-lg-4 form button[type=submit]')]);
  const newDisc = Number(p.url().split('/').pop());
  await p.fill('input[name=topicTitle]', 'Основы сетей');
  await Promise.all([p.waitForNavigation(), p.click('form[action*="AddTopic"] button')]);
  await p.fill('input[name=lectureTitle]', 'Модель OSI');
  await Promise.all([p.waitForNavigation(), p.click('form[action*="AddLecture"] button')]);
  const newLec = Number(p.url().split('/').pop().split('?')[0]);
  await p.fill('#Input_Content', '# Модель OSI\nСемь уровней: физический, канальный, сетевой, транспортный, сеансовый, представления, прикладной.');
  await Promise.all([p.waitForNavigation(), p.click('#main form button[type=submit]')]);
  await p.goto(`${B}/Teacher/Question?lecture=${newLec}&kind=test`);
  await p.fill('#Text', 'Сколько уровней в модели OSI?'); await p.selectOption('#Type', '1');
  await p.locator('#accepts input[name=AcceptText]').first().fill('7');
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
  check('Д4', 'Управление дисциплинами', 'Создание дисциплины, темы, лекции и вопроса теста', (await p.textContent('main')).includes('Сколько уровней в модели OSI?'));
  await p.goto(`${B}/Teacher/Question?lecture=${newLec}&kind=test`);
  await p.fill('#Text', 'Проверка'); await p.selectOption('#Type', '2');
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
  check('Д5', 'Управление дисциплинами', 'Редактор вопроса не сохраняет вопрос без правильного варианта', (await p.textContent('.validation-summary-errors')).includes('варианта'));
  // Удаление лекции вместе с тестом и вариантами ответов
  await p.goto(`${B}/Teacher/Discipline/${newDisc}`);
  await p.fill('input[name=lectureTitle]', 'Временная лекция');
  await Promise.all([p.waitForNavigation(), p.click('form[action*="AddLecture"] button')]);
  const tmpLec = Number(p.url().split('/').pop().split('?')[0]);
  await p.goto(`${B}/Teacher/Question?lecture=${tmpLec}&kind=test`);
  await p.fill('#Text', 'Временный вопрос'); await p.selectOption('#Type', '2');
  await p.locator('#choices input[name=ChoiceText]').nth(0).fill('Да'); await p.locator('#choices input[name=ChoiceText]').nth(1).fill('Нет');
  await p.locator('#choices input[name=ChoiceCorrect]').first().check();
  await Promise.all([p.waitForNavigation(), p.click('main form button[type=submit]')]);
  await p.goto(`${B}/Teacher/Lecture/${tmpLec}`);
  await p.locator('form[action*="handler=Delete"] button').first().click(); await confirmModal(p);
  check('Д5б', 'Управление дисциплинами', 'Удаление лекции вместе с тестом и вариантами ответов', (await p.textContent('.alert-success')).includes('удалена') && sql(`select count(*) from Lectures where Id=${tmpLec}`).includes('(0,)'));
  await logout(p);

  // ---------- 2. Авторизация студента ----------
  await login(p, 'orlov', 'Student123');
  check(2, 'Авторизация', 'Авторизация студента: открывается личный кабинет', p.url().endsWith('/Study') && (await p.textContent('h1')).includes('Мои дисциплины'));
  await shot(p, 'kabinet_studenta');
  await p.goto(B + '/Study/Discipline/1'); await shot(p, 'disciplina_studenta_nachalo');

  // ---------- 3. Следующая лекция без теста ----------
  let r = await p.goto(B + '/Study/Lecture/2');
  check(3, 'Последовательность', 'Попытка открыть следующую лекцию без теста: доступ запрещён', r.status() === 403 && (await p.textContent('main')).includes('пока закрыта'), `HTTP ${r.status()}`);
  await shot(p, 'lekciya_zakryta');

  // Лекция 1: теория и контрольные вопросы
  await p.goto(B + '/Study/Lecture/1'); await shot(p, 'lekciya_teoriya');
  const a1 = answersOf(1);
  await p.goto(B + '/Study/Lecture/1?tab=control');
  for (const q of a1.control) await fill(p, q, 'right');
  await Promise.all([p.waitForNavigation(), p.click('#control form button[type=submit]')]);
  const ctrlMsg = (await p.locator('.alert').allTextContents()).join(' ');
  check('Д6', 'Контрольные вопросы', 'Автопроверка ответов и отправка развёрнутого ответа на проверку', ctrlMsg.includes('верно: 2') && ctrlMsg.includes('на проверку преподавателю: 1'), ctrlMsg.trim().slice(0, 120));
  await shot(p, 'kontrolnye_voprosy_otvety');

  // ---------- 4. Тест ниже порога ----------
  await takeTest(p, 1, 2);
  await shot(p, 'test_prohozhdenie');
  await submitTest(p);
  check(4, 'Последовательность', 'Тест с результатом ниже порога: результат 33% < 70%, следующая лекция закрыта',
    (await p.textContent('main')).includes('Тест не сдан') && (await p.request.get(B + '/Study/Lecture/2', { maxRedirects: 0 })).status() === 403);
  await shot(p, 'test_ne_sdan');

  // ---------- 5. Тест с требуемым результатом ----------
  await takeTest(p, 1, 99); await submitTest(p);
  const passedText = await p.textContent('main');
  const stillLocked = (await p.request.get(B + '/Study/Lecture/2', { maxRedirects: 0 })).status() === 403;
  await shot(p, 'test_sdan');
  check('5а', 'Последовательность', 'Тест сдан, но развёрнутый ответ ещё не проверен — следующая лекция пока закрыта', passedText.includes('Тест сдан') && stillLocked);
  await logout(p);
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Reviews'); await shot(p, 'proverka_otvetov');
  const rev = p.locator('.card').filter({ hasText: 'Орлов Дмитрий' }).first();
  await rev.locator('input[name=comment]').fill('Верно, пример с полями хороший.');
  await Promise.all([p.waitForNavigation(), rev.locator('button[value=true]').click()]);
  check('Д7', 'Контрольные вопросы', 'Преподаватель принимает развёрнутый ответ', (await p.textContent('.alert-success')).includes('открыта следующая'));
  await logout(p);
  await login(p, 'orlov', 'Student123');
  r = await p.goto(B + '/Study/Lecture/2');
  check(5, 'Последовательность', 'Тест сдан и остальные условия выполнены: следующая лекция доступна', r.status() === 200 && (await p.textContent('h1')).includes('Лекция 2'));
  await p.goto(B + '/Study/Discipline/1'); await shot(p, 'disciplina_studenta_progress');

  // ---------- 6. Повторный вход ----------
  await logout(p);
  const ctx2 = await browser.newContext({ viewport: { width: 1366, height: 860 } }); const p2 = await ctx2.newPage();
  await login(p2, 'orlov', 'Student123');
  await p2.goto(B + '/Study/Discipline/1');
  check(6, 'Прогресс', 'Повторный вход (новый браузер): прогресс сохранён', (await p2.textContent('main')).includes('20%') && (await p2.locator('.state-completed').count()) === 1);
  await p2.goto(B + '/Study/Results'); await shot(p2, 'moi_rezultaty');

  // ---------- 7. Студент открывает панель преподавателя ----------
  r = await p2.goto(B + '/Teacher');
  check(7, 'Права доступа', 'Студент открывает панель преподавателя: доступ запрещён', p2.url().includes('/Account/AccessDenied') && (await p2.textContent('h1')).includes('Доступ запрещён'));
  await shot(p2, 'dostup_zapreschen');
  r = await p2.request.post(B + '/Teacher/Users?handler=Delete&id=1', { maxRedirects: 0 });
  check('Д8', 'Права доступа', 'Студент не может выполнить действие преподавателя запросом в обход интерфейса', r.status() === 302 || r.status() === 400 || r.status() === 403, `HTTP ${r.status()}`);

  // ---------- 12. Прямые ссылки на закрытую лекцию ----------
  const lockedResp = await p2.request.get(B + '/Study/Lecture/5', { maxRedirects: 0 });
  const fileResp = await p2.request.get(B + '/Materials/4', { maxRedirects: 0 });
  const token = await p2.locator('input[name=__RequestVerificationToken]').first().inputValue().catch(() => '');
  await p2.goto(B + '/Study/Lecture/2');
  const tok = await p2.locator('input[name=__RequestVerificationToken]').first().inputValue();
  const startResp = await p2.request.post(B + '/Study/Lecture/5?handler=Start', { form: { __RequestVerificationToken: tok }, maxRedirects: 0 });
  const ctrlResp = await p2.request.post(B + '/Study/Lecture/5?handler=Control', { form: { __RequestVerificationToken: tok }, maxRedirects: 0 });
  check(12, 'Права доступа', 'Прямые ссылки на закрытую лекцию: страница, файл, ответы и тест отклоняются сервером',
    lockedResp.status() === 403 && [302, 403].includes(fileResp.status()) && [302, 403].includes(startResp.status()) && [302, 403].includes(ctrlResp.status()),
    `страница ${lockedResp.status()}, файл ${fileResp.status()}${fileResp.status() === 302 ? '→«Доступ запрещён»' : ''}, тест ${startResp.status()}, ответы ${ctrlResp.status()}`);
  await ctx2.close();

  // ---------- Попытки и время ----------
  await login(p, 'sidorov', 'Student123');
  await takeTest(p, 1, 1); await submitTest(p);
  await p.goto(B + '/Study/Lecture/1?tab=test');
  const noAttempts = (await p.textContent('#test')).includes('Попытки закончились');
  await shot(p, 'popytki_zakonchilis');
  const tok2 = await p.locator('input[name=__RequestVerificationToken]').first().inputValue();
  await p.request.post(B + '/Study/Lecture/1?handler=Start', { form: { __RequestVerificationToken: tok2 } });
  const startedAnyway = sql("select count(*) from TestAttempts a join Users u on u.Id=a.StudentId where u.Login='sidorov' and a.Status=1");
  check('Д9', 'Тесты', 'Попытки закончились: новую попытку начать нельзя даже запросом в обход кнопки', noAttempts && startedAnyway.includes('(0,)'), startedAnyway);
  await logout(p);
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Students'); await shot(p, 'spisok_studentov');
  const sid = sql("select Id from Users where Login='sidorov'").match(/\d+/)[0];
  await p.goto(B + '/Teacher/Student/' + sid);
  await Promise.all([p.waitForNavigation(), p.locator('tr', { hasText: 'Основы тестирования' }).first().locator('form[action*="Attempt"] button').click()]);
  check('Д10', 'Панель преподавателя', 'Преподаватель выдаёт дополнительную попытку (записывается в журнал)', (await p.textContent('.alert-success')).includes('ещё одну попытку'));
  await shot(p, 'kartochka_studenta');
  await logout(p);
  await login(p, 'sidorov', 'Student123');
  await takeTest(p, 1, 0);
  const attemptId = p.url().split('/').pop();
  sql(`update TestAttempts set Deadline=datetime('now','localtime','-10 minutes') where Id=${attemptId}`);
  await submitTest(p);
  check('Д11', 'Тесты', 'Ответы после окончания времени не засчитываются (проверка на сервере)', (await p.textContent('main')).includes('Время вышло'));
  await logout(p);

  // ---------- 8. Замена учебного материала ----------
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Lecture/3?tab=material');
  const v2 = path.join(OUT, 'lecture3_v2.pdf');
  const tmp = await browser.newPage(); await tmp.setContent('<h1>Лекция 3. Тест-кейсы — версия 2 (обновлено)</h1><p>Дополнен раздел о регрессионном тестировании.</p>'); await tmp.pdf({ path: v2 }); await tmp.close();
  await p.setInputFiles('#Upload', v2);
  await Promise.all([p.waitForNavigation(), p.click('#material form[enctype] button[type=submit]')]);
  await shot(p, 'zamena_materiala');
  await logout(p);
  await login(p, 'kuznetsova', 'Student123');
  const got = await p.request.get(B + '/Materials/3');
  const same = Buffer.compare(Buffer.from(await got.body()), fs.readFileSync(v2)) === 0;
  await p.goto(B + '/Study/Lecture/3?tab=material');
  check(8, 'Материалы', 'Замена учебного материала: студенту доступна новая версия', same && (await p.textContent('#material')).includes('версия 2') && got.headers()['cache-control'].includes('no-store'));
  await shot(p, 'material_pdf');
  await p.goto(B + '/Study/Lecture/4?tab=material'); await shot(p, 'material_docx');
  await logout(p);

  // ---------- 9. Одновременная работа нескольких студентов ----------
  const many = ['petrova', 'kuznetsova', 'student', 'novikov', 'orlov'];
  const pass = { novikov: 'Study2026' };
  const t0 = Date.now();
  const runs = await Promise.all(many.map(async u => {
    const c = await browser.newContext(); const pg = await c.newPage();
    try {
      await login(pg, u, pass[u] || 'Student123');
      await pg.goto(B + '/Study/Discipline/2'); await pg.goto(B + '/Study/Lecture/6');
      await takeTest(pg, 6, 99); await submitTest(pg);
      const ok = (await pg.textContent('main')).includes('Тест сдан');
      await c.close(); return ok;
    } catch (e) { await c.close(); return false; }
  }));
  check(9, 'Нагрузка', 'Одновременная работа 5 студентов: все прошли тест, ошибок нет', runs.every(Boolean), `${runs.filter(Boolean).length} из ${many.length} за ${((Date.now() - t0) / 1000).toFixed(1)} с`);

  // ---------- 11. Резервное копирование и восстановление ----------
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Backup');
  await Promise.all([p.waitForNavigation(), p.click('form[action*="Create"] button')]);
  const backupName = (await p.locator('tbody tr td').first().textContent()).trim();
  await shot(p, 'rezervnye_kopii');
  // «Сбой»: удаляем дисциплину
  await p.goto(B + '/Teacher/Disciplines');
  await p.locator('tr', { hasText: 'Компьютерные сети' }).locator('form[action*="Delete"] button').click(); await confirmModal(p);
  const deleted = !(await p.textContent('tbody')).includes('Компьютерные сети');
  await p.goto(B + '/Teacher/Backup');
  await p.locator('tr', { hasText: backupName }).locator('form[action*="Restore"] button').click(); await confirmModal(p);
  await p.goto(B + '/Teacher/Disciplines');
  check(11, 'Сопровождение', 'Резервное копирование и восстановление: удалённая дисциплина вернулась из копии', deleted && (await p.textContent('tbody')).includes('Компьютерные сети'), backupName);
  await logout(p);

  // ---------- 10. Перезапуск приложения ----------
  await stopServer(); await startServer();
  await login(p, 'orlov', 'Student123');
  await p.goto(B + '/Study/Discipline/1');
  const afterRestart = (await p.textContent('main')).includes('20%');
  const mat = await p.request.get(B + '/Study/Lecture/2');
  check(10, 'Сопровождение', 'Перезапуск приложения: сайт запускается, прогресс и данные на месте', afterRestart && mat.status() === 200);
  await logout(p);

  // ---------- Отчёты, журнал, состояние ----------
  await login(p, 'teacher', 'Teacher123');
  await p.goto(B + '/Teacher/Report?discipline=1'); await shot(p, 'otchet_po_gruppe');
  const csv = await p.request.get(B + '/Teacher/Report?handler=Csv&discipline=1');
  check('Д12', 'Отчёты', 'Отчёт по дисциплине и группе, выгрузка в Excel (CSV)', csv.status() === 200 && (await csv.text()).includes('Орлов Дмитрий'));
  await p.goto(B + '/Teacher/Report?discipline=1&topic=1');
  check('Д13', 'Отчёты', 'Фильтр «не завершили тему» показывает отстающих', !(await p.textContent('tbody')).includes('Кузнецова Мария') && (await p.textContent('tbody')).includes('Сидоров Павел'));
  await p.goto(B + '/Teacher/Journal'); const journal = await p.textContent('main');
  check('Д14', 'Журнал', 'Действия преподавателя фиксируются в журнале событий', journal.includes('Выдана дополнительная попытка') && journal.includes('Ответ принят') && journal.includes('Материал заменён') && journal.includes('Данные восстановлены'));
  await shot(p, 'zhurnal_sobytiy');
  await p.goto(B + '/Teacher/Users'); await shot(p, 'uchetnye_zapisi');
  await p.goto(B + '/Teacher/System'); await shot(p, 'sostoyanie_sistemy');
  const health = await (await fetch(B + '/health')).json();
  check('Д15', 'Сопровождение', 'Проверка доступности /health: сайт и база данных работают', health.status === 'ok');

  // ---------- Телефон ----------
  const mctx = await browser.newContext({ ...devices['iPhone 13'] }); const m = await mctx.newPage();
  await login(m, 'student', 'Student123');
  let wide = [];
  for (const u of ['/Study', '/Study/Discipline/1', '/Study/Lecture/1', '/Study/Results']) {
    await m.goto(B + u);
    const [vw, dw] = await m.evaluate(() => [innerWidth, document.documentElement.scrollWidth]);
    if (dw > vw) wide.push(u);
  }
  await m.goto(B + '/Study/Discipline/1'); await shot(m, 'telefon', false);
  check('Д16', 'Интерфейс', 'Страницы студента помещаются на экране телефона', wide.length === 0, wide.join(', '));
  await mctx.close();

  // Светлая и тёмная тема: переключение запоминается после перезагрузки
  await logout(p);
  await login(p, 'student', 'Student123');
  await p.goto(B + '/Study/Lecture/1?tab=control');
  await p.click('[data-theme-toggle]');
  await p.reload();
  const darkKept = await p.evaluate(() => document.documentElement.getAttribute('data-bs-theme')) === 'dark';
  await shot(p, 'temnaya_tema');
  await p.click('[data-theme-toggle]'); await p.reload();
  const lightBack = await p.evaluate(() => document.documentElement.getAttribute('data-bs-theme')) === 'light';
  check('Д18', 'Интерфейс', 'Переключение светлой и тёмной темы, выбор сохраняется после перезагрузки', darkKept && lightBack);
  await logout(p);
  // Шапка помещается на распространённых ширинах экрана
  const narrow = [];
  for (const w of [1200, 1366, 1920]) {
    await p.setViewportSize({ width: w, height: 860 });
    await login(p, 'teacher', 'Teacher123');
    const right = await p.evaluate(() => document.querySelector('nav form button').getBoundingClientRect().right);
    if (right > w) narrow.push(w);
    await logout(p);
  }
  await p.setViewportSize({ width: 1366, height: 860 });
  check('Д19', 'Интерфейс', 'Меню преподавателя помещается на экранах 1200, 1366 и 1920 пикселей', narrow.length === 0, narrow.join(', '));

  check('Д17', 'Интерфейс', 'Нет ошибок JavaScript на страницах', jsErrors.length === 0, jsErrors.join('; '));
  fs.writeFileSync(`${OUT}/results.json`, JSON.stringify(results, null, 1));
  const failed = results.filter(r => !r.ok).length;
  console.log(failed ? `ОШИБОК: ${failed} из ${results.length}` : `ВСЕ ${results.length} ПРОВЕРОК ПРОЙДЕНЫ`);
  await browser.close(); await stopServer();
})().catch(async e => { console.error(e); try { await stopServer(); } catch { } process.exit(1); });
