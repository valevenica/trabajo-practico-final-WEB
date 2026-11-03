using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StockManufactura.Application.Interfaces;
using StockManufactura.Application.Resources;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace StockManufactura.Infrastructure.Import
{
    public sealed class PdfPigFacturaReader : IFacturaPdfReader
    {
        public FacturaLeida Leer(Stream pdf)
        {
            using var documento = PdfDocument.Open(pdf);
            var lineas = new List<string>();
            foreach (var pagina in documento.GetPages())
            {
                // AFIP emite original, duplicado y triplicado en el mismo PDF: se lee solo el original.
                if (pagina.Number > 1 && (pagina.Text.Contains("DUPLICADO", StringComparison.OrdinalIgnoreCase)
                    || pagina.Text.Contains("TRIPLICADO", StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                lineas.AddRange(ArmarLineas(pagina.GetWords()));
            }

            if (lineas.Count == 0)
            {
                throw new InvalidOperationException("El PDF no tiene texto para leer (¿es una imagen escaneada?). Usá la factura electrónica original.");
            }

            return FacturaTextoParser.Parsear(lineas);
        }

        /// <summary>Agrupa las palabras que comparten renglón (misma altura) y las ordena de izquierda a derecha.</summary>
        private static IEnumerable<string> ArmarLineas(IEnumerable<Word> palabras)
        {
            const double tolerancia = 3;
            var renglones = new List<(double Y, List<Word> Palabras)>();
            foreach (var palabra in palabras.OrderByDescending(p => p.BoundingBox.Bottom))
            {
                var y = palabra.BoundingBox.Bottom;
                var renglon = renglones.FirstOrDefault(r => Math.Abs(r.Y - y) <= tolerancia);
                if (renglon.Palabras is null)
                {
                    renglones.Add((y, new List<Word> { palabra }));
                }
                else
                {
                    renglon.Palabras.Add(palabra);
                }
            }

            foreach (var (_, grupo) in renglones)
            {
                var ordenadas = grupo.OrderBy(p => p.BoundingBox.Left).ToList();
                var texto = ordenadas[0].Text;
                for (var i = 1; i < ordenadas.Count; i++)
                {
                    // Un hueco grande separa columnas: se marca con doble espacio.
                    var hueco = ordenadas[i].BoundingBox.Left - ordenadas[i - 1].BoundingBox.Right;
                    texto += (hueco > 12 ? "  " : " ") + ordenadas[i].Text;
                }

                yield return texto;
            }
        }
    }
}
