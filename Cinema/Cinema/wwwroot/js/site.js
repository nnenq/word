// Окно подтверждения вместо стандартного confirm/alert браузера
const modal = (() => {
    const box = document.getElementById("modal");
    const text = document.getElementById("modalText");
    const ok = box.querySelector("[data-modal=ok]");
    const cancel = box.querySelector("[data-modal=cancel]");
    let done = null;
    function close(result) {
        box.hidden = true;
        document.body.classList.remove("no-scroll");
        if (done) done(result);
        done = null;
    }
    ok.addEventListener("click", () => close(true));
    cancel.addEventListener("click", () => close(false));
    box.addEventListener("click", e => { if (e.target === box) close(false); });
    document.addEventListener("keydown", e => { if (!box.hidden && e.key === "Escape") close(false); });
    return {
        // onlyOk = true — простое сообщение с одной кнопкой «Понятно»
        show(message, onlyOk) {
            text.textContent = message;
            cancel.hidden = !!onlyOk;
            ok.textContent = onlyOk ? "Понятно" : "Да";
            box.hidden = false;
            document.body.classList.add("no-scroll");
            ok.focus();
            return new Promise(resolve => done = resolve);
        },
    };
})();

// Подтверждение удаления и возврата: текст берётся у нажатой кнопки или у формы
document.addEventListener("submit", async e => {
    const form = e.target;
    const msg = (e.submitter && e.submitter.dataset.confirm) || (form.dataset && form.dataset.confirm);
    if (!msg || form.dataset.confirmed) return;
    e.preventDefault();
    if (await modal.show(msg)) {
        form.dataset.confirmed = "1";
        form.requestSubmit ? form.requestSubmit(e.submitter) : form.submit();
    }
});

// Кнопка меню: на широком экране сворачивает меню до значков, на узком — открывает его поверх страницы
(() => {
    const burger = document.querySelector(".burger");
    const scrim = document.querySelector(".scrim");
    const body = document.body;
    try { if (localStorage.getItem("menu-mini") === "1") body.classList.add("mini"); } catch { }
    function setOpen(open) {
        body.classList.toggle("menu-open", open);
        scrim.hidden = !open;
        burger.setAttribute("aria-expanded", open);
    }
    burger.addEventListener("click", () => {
        if (innerWidth >= 1200) {
            const mini = body.classList.toggle("mini");
            try { localStorage.setItem("menu-mini", mini ? "1" : "0"); } catch { }
        } else setOpen(!body.classList.contains("menu-open"));
    });
    scrim.addEventListener("click", () => setOpen(false));
    addEventListener("resize", () => { if (innerWidth >= 1200) setOpen(false); });
    document.addEventListener("keydown", e => { if (e.key === "Escape") setOpen(false); });

    // Поиск на телефоне открывается значком лупы
    const input = document.querySelector(".search input");
    document.querySelector(".search-open").addEventListener("click", () => { body.classList.add("search-open"); input.focus(); });
    document.querySelector(".search-back").addEventListener("click", () => body.classList.remove("search-open"));
})();

// Подписи к ячейкам таблиц: на телефоне строки таблицы показываются карточками
document.querySelectorAll(".main table").forEach(table => {
    const heads = [...table.querySelectorAll("thead th")].map(th => th.textContent.trim());
    table.querySelectorAll("tbody tr").forEach(tr =>
        [...tr.children].forEach((td, i) => { if (heads[i]) td.dataset.label = heads[i]; }));
});

// Возврат нескольких заказов: галочки, строка внизу с количеством и суммой
const rub = n => n.toLocaleString("ru-RU") + " ₽";
const plural = (n, one, few, many) => (n % 100 >= 11 && n % 100 <= 14) ? many : [many, one, few, few, few, many, many, many, many, many][n % 10];
document.querySelectorAll("[data-bulk]").forEach(form => {
    const boxes = [...form.querySelectorAll("input[name=ids]")];
    const all = form.querySelector("[data-check-all]");
    const bar = form.querySelector(".bulk-bar");
    const text = form.querySelector(".bulk-text");
    const submit = form.querySelector("[data-bulk-submit]");
    if (!boxes.length) { all.disabled = true; return; }
    function update() {
        const chosen = boxes.filter(b => b.checked);
        const count = chosen.reduce((s, b) => s + Number(b.dataset.count), 0);
        const sum = chosen.reduce((s, b) => s + Number(b.dataset.price), 0);
        bar.hidden = chosen.length === 0;
        text.textContent = `Выбрано: ${count} ${plural(count, "билет", "билета", "билетов")} · вернуть ${rub(sum)}`;
        submit.dataset.confirm = `Оформить возврат: ${chosen.length} ${plural(chosen.length, "заказ", "заказа", "заказов")}, ${count} ${plural(count, "билет", "билета", "билетов")}? Вернуть покупателям ${rub(sum)}.`;
        all.checked = chosen.length === boxes.length;
        all.indeterminate = chosen.length > 0 && chosen.length < boxes.length;
        boxes.forEach(b => b.closest("tr").classList.toggle("row-selected", b.checked));
    }
    boxes.forEach(b => b.addEventListener("change", update));
    all.addEventListener("change", () => { boxes.forEach(b => b.checked = all.checked); update(); });
    form.querySelector("[data-clear]").addEventListener("click", () => { boxes.forEach(b => b.checked = false); update(); });
    update();
});

// Сообщения об успехе и ошибке плавно исчезают через несколько секунд
document.querySelectorAll(".alert-ok").forEach(a => setTimeout(() => a.classList.add("fade"), 6000));

// Схема зала: подсчёт выбранных мест и суммы заказа
(() => {
    const form = document.querySelector("[data-buy]");
    if (!form) return;
    const price = Number(form.dataset.price);
    const max = Number(form.dataset.max || 0);
    const list = document.getElementById("chosen");
    const total = document.getElementById("total");
    const submit = document.getElementById("buyBtn");
    const boxes = [...form.querySelectorAll(".seat input:not(:disabled)")];

    function update(changed) {
        const chosen = boxes.filter(b => b.checked);
        if (max && chosen.length > max && changed) {
            changed.checked = false;
            modal.show("За один заказ можно выбрать не больше " + max + " мест.", true);
            return update();
        }
        list.textContent = chosen.length
            ? chosen.map(b => { const [r, s] = b.value.split("-"); return "ряд " + r + ", место " + s; }).join("; ")
            : "Места не выбраны";
        total.textContent = (chosen.length * price).toLocaleString("ru-RU") + " ₽";
        submit.disabled = chosen.length === 0;
    }
    boxes.forEach(b => b.addEventListener("change", () => update(b)));
    update();
})();
