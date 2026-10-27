(() => {
    "use strict";
    const kind = document.getElementById("Tur"), source = document.getElementById("KaynakAnaDal");
    if (!kind || !source) return;
    const options = [...source.options].filter(o => o.value), placeholder = source.options[0];
    function update() {
        const previous = source.value;
        source.replaceChildren(placeholder, ...options.filter(o => o.dataset.type === kind.value));
        source.value = [...source.options].some(o => o.value === previous) ? previous : "";
    }
    kind.addEventListener("change", update); update();
})();
