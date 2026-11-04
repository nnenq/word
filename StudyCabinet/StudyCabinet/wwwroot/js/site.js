// Подтверждение действий: форма с data-confirm или кнопка с data-confirm показывает окно «Да / Отмена»
(() => {
    const el = document.getElementById("confirmModal");
    if (!el) return;
    const modal = new bootstrap.Modal(el);
    let pending = null;
    document.addEventListener("submit", e => {
        const msg = (e.submitter && e.submitter.dataset.confirm) || e.target.dataset.confirm;
        if (!msg || e.target.dataset.confirmed) return;
        e.preventDefault();
        pending = { form: e.target, submitter: e.submitter };
        document.getElementById("confirmText").textContent = msg;
        modal.show();
    });
    document.getElementById("confirmOk").addEventListener("click", () => {
        if (!pending) return;
        pending.form.dataset.confirmed = "1";
        modal.hide();
        pending.form.requestSubmit ? pending.form.requestSubmit(pending.submitter) : pending.form.submit();
    });
})();

// Таймер теста: показывает оставшееся время и отправляет ответы, когда время вышло
(() => {
    const t = document.querySelector("[data-deadline]");
    if (!t) return;
    const deadline = Date.now() + Number(t.dataset.left) * 1000;
    const form = document.getElementById(t.dataset.form);
    function tick() {
        const left = Math.max(0, Math.round((deadline - Date.now()) / 1000));
        t.textContent = String(Math.floor(left / 60)).padStart(2, "0") + ":" + String(left % 60).padStart(2, "0");
        t.classList.toggle("low", left <= 60);
        if (left === 0) { form.dataset.confirmed = "1"; form.requestSubmit(); return; }
        setTimeout(tick, 500);
    }
    tick();
})();

// Предупреждение при уходе со страницы теста без отправки
(() => {
    const form = document.querySelector("form[data-test]");
    if (!form) return;
    let changed = false;
    form.addEventListener("change", () => changed = true);
    form.addEventListener("submit", () => changed = false);
    window.addEventListener("beforeunload", e => { if (changed) { e.preventDefault(); e.returnValue = ""; } });
})();

// Редактор вопроса: показывает нужные поля для выбранного типа и добавляет строки вариантов
(() => {
    const type = document.querySelector("[data-question-type]");
    if (!type) return;
    function sync() {
        const v = type.value;
        document.querySelectorAll("[data-for-types]").forEach(b => b.hidden = !b.dataset.forTypes.split(",").includes(v));
    }
    type.addEventListener("change", sync);
    sync();
    let counter = 0;
    document.querySelectorAll("[data-add-row]").forEach(btn => btn.addEventListener("click", () => {
        const box = document.getElementById(btn.dataset.addRow);
        const tpl = box.querySelector("template");
        // У каждой новой строки свой ключ: по нему сервер понимает, какой вариант отмечен правильным
        const key = "n" + Date.now() + "_" + (++counter);
        box.querySelector(".rows").insertAdjacentHTML("beforeend", tpl.innerHTML.replace(/__KEY__/g, key));
        box.querySelector(".rows .opt-row:last-child input")?.focus();
    }));
    document.addEventListener("click", e => {
        const del = e.target.closest("[data-remove-row]");
        if (del) del.closest(".opt-row").remove();
    });
})();

// Включение подсказок Bootstrap
document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => new bootstrap.Tooltip(el));
