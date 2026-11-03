// Funciones del navegador que usan las páginas interactivas.
window.stockManufactura = {
    // El BOM hace que Excel abra el CSV con acentos correctos.
    descargar: function (nombre, contenido, tipo) {
        guardar(nombre, new Blob(["\uFEFF" + contenido], { type: tipo }));
    },
    // Blazor envía byte[] como Uint8Array.
    descargarBytes: function (nombre, contenido, tipo) {
        guardar(nombre, new Blob([contenido], { type: tipo }));
    }
};

function guardar(nombre, blob) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = nombre;
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
}

// En celular, cerrar el menú plegable al elegir una opción (la navegación no recarga el layout).
document.addEventListener("click", function (e) {
    if (e.target.closest(".nav a")) {
        const toggle = document.getElementById("nav-toggle");
        if (toggle) { toggle.checked = false; }
    }

    // En pantalla grande, ocultar o mostrar el menú lateral; la elección se recuerda en este navegador.
    const boton = e.target.closest("[data-menu]");
    if (boton) {
        const ocultar = boton.dataset.menu === "ocultar";
        try { localStorage.setItem("menuOculto", ocultar ? "1" : "0"); } catch { }
        aplicarMenu(ocultar);
    }
});

function aplicarMenu(ocultar) {
    document.documentElement.classList.toggle("menu-oculto", ocultar);
}

function restaurarMenu() {
    let oculto = false;
    try { oculto = localStorage.getItem("menuOculto") === "1"; } catch { }
    aplicarMenu(oculto);
}

restaurarMenu();
document.addEventListener("DOMContentLoaded", function () {
    // La navegación mejorada de Blazor puede reemplazar atributos del documento: se vuelve a aplicar tras cada página.
    if (window.Blazor) { Blazor.addEventListener("enhancedload", restaurarMenu); }
});
