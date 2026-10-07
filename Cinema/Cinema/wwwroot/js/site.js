// Схема зала: подсчёт выбранных мест и суммы заказа
document.addEventListener("DOMContentLoaded", () => {
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
            alert("За один заказ можно выбрать не больше " + max + " мест.");
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
});

// Подтверждение удаления и возврата
document.addEventListener("submit", e => {
    const msg = e.target.dataset && e.target.dataset.confirm;
    if (msg && !confirm(msg)) e.preventDefault();
});
