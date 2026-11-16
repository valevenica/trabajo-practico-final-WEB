using Microsoft.Playwright;

/// <summary>
/// El sistema pide confirmación con un cuadro propio dentro de la página (no con confirm/prompt del navegador).
/// Este script lo responde solo, como antes se aceptaban las ventanas del navegador: acepta las confirmaciones y,
/// cuando se pide escribir una frase, escribe la que indique <c>window.__fraseDialogo</c>; si no coincide, cancela.
/// </summary>
public static class DialogosPagina
{
    private const string Script = """
        (() => {
            const responder = () => {
                if (window.__dialogoManual) { return; }
                const dialogo = document.querySelector('.dialogo:not([data-respondido])');
                if (!dialogo) { return; }
                dialogo.setAttribute('data-respondido', '1');
                const entrada = dialogo.querySelector('.dialogo-frase input');
                if (entrada) {
                    const frase = dialogo.querySelector('.dialogo-frase strong').textContent.trim();
                    if ((window.__fraseDialogo ?? '') !== frase) {
                        setTimeout(() => dialogo.querySelector('[data-dialogo=cancelar]').click(), 150);
                        return;
                    }
                    entrada.value = frase;
                    entrada.dispatchEvent(new Event('input', { bubbles: true }));
                }
                // Se espera a que Blazor habilite el botón tras escribir la frase.
                const aceptar = () => {
                    const boton = dialogo.querySelector('[data-dialogo=aceptar]');
                    if (boton && !boton.disabled) { boton.click(); } else if (boton) { setTimeout(aceptar, 100); }
                };
                setTimeout(aceptar, 150);
            };
            new MutationObserver(responder).observe(document, { childList: true, subtree: true });
        })();
        """;

    public static Task ResponderSolosAsync(IPage pagina) => pagina.AddInitScriptAsync(Script);

    /// <summary>Con <c>true</c>, el cuadro queda abierto para que la prueba lo inspeccione y lo responda.</summary>
    public static Task ManualAsync(IPage pagina, bool manual)
        => pagina.EvaluateAsync("m => { window.__dialogoManual = m; }", manual);

    public static Task FraseAsync(IPage pagina, string? frase)
        => pagina.EvaluateAsync("f => { window.__fraseDialogo = f; }", frase);
}
