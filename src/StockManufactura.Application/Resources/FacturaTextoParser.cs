using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace StockManufactura.Application.Resources
{
    /// <summary>
    /// Interpreta el texto de una factura electrónica de AFIP (formato de «Comprobantes en línea»), renglón por renglón.
    /// Cada renglón de detalle tiene la forma «Código Descripción Cantidad [Unidad] PrecioUnit. %Bonif ImpBonif Subtotal
    /// [Alícuota Subtotal c/IVA]»; se reconoce por la cola de importes, aunque la descripción tenga números.
    /// </summary>
    public static class FacturaTextoParser
    {
        private static readonly Regex Cuit = new(@"CUIT:?\s*(\d{2}-?\d{8}-?\d)", RegexOptions.IgnoreCase);
        private static readonly Regex RazonSocial = new(@"Raz[oó]n\s+Social:?\s*(.+?)(?:\s{2,}|\s+Fecha|\s+Domicilio|$)", RegexOptions.IgnoreCase);
        private static readonly Regex Comprobante = new(@"Punto\s+de\s+Venta:?\s*(\d+)\s+Comp\.?\s*Nro:?\s*(\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex FechaEmision = new(@"Fecha\s+de\s+Emisi[oó]n:?\s*(\d{2}/\d{2}/\d{4})", RegexOptions.IgnoreCase);
        private static readonly Regex Importe = new(@"^-?\d{1,3}(?:\.\d{3})*(?:,\d+)?$|^-?\d+(?:[.,]\d+)?$");
        private static readonly Regex Porcentaje = new(@"^\d+(?:[.,]\d+)?%$");
        private static readonly Regex FinDeDetalle = new(@"^(Subtotal|Importe\s+(Neto|Total|Otros)|Otros\s+Tributos|Total:|CAE)", RegexOptions.IgnoreCase);

        public static FacturaLeida Parsear(IEnumerable<string> lineasTexto)
        {
            var lineas = lineasTexto.Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
            var factura = new FacturaLeida();

            foreach (var linea in lineas)
            {
                if (factura.CuitEmisor.Length == 0 && Cuit.Match(linea) is { Success: true } cuit)
                {
                    factura.CuitEmisor = SoloDigitos(cuit.Groups[1].Value);
                }

                if (factura.RazonSocialEmisor.Length == 0 && RazonSocial.Match(linea) is { Success: true } razon)
                {
                    factura.RazonSocialEmisor = razon.Groups[1].Value.Trim();
                }

                if (factura.NumeroComprobante.Length == 0 && Comprobante.Match(linea) is { Success: true } comp)
                {
                    factura.NumeroComprobante = $"{comp.Groups[1].Value}-{comp.Groups[2].Value}";
                }

                if (factura.Fecha is null && FechaEmision.Match(linea) is { Success: true } fecha
                    && DateTime.TryParseExact(fecha.Groups[1].Value, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f))
                {
                    factura.Fecha = f;
                }
            }

            // El detalle va desde el encabezado de la tabla hasta los totales; sin encabezado se prueba con todo el texto.
            var inicio = lineas.FindIndex(x => x.Contains("Cantidad", StringComparison.OrdinalIgnoreCase) && x.Contains("Precio", StringComparison.OrdinalIgnoreCase));
            foreach (var linea in lineas.Skip(inicio + 1))
            {
                if (inicio >= 0 && FinDeDetalle.IsMatch(linea))
                {
                    break;
                }

                if (TryParsearRenglon(linea, out var renglon))
                {
                    factura.Renglones.Add(renglon);
                }
            }

            return factura;
        }

        public static bool TryParsearRenglon(string linea, out FacturaRenglon renglon)
        {
            renglon = new FacturaRenglon();
            var tokens = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // La cantidad es el primer número tras el cual (salteando la unidad de medida) solo quedan importes: al menos
            // el precio unitario y el subtotal.
            for (var i = 1; i < tokens.Length; i++)
            {
                if (!EsImporte(tokens[i]))
                {
                    continue;
                }

                var j = i + 1;
                while (j < tokens.Length && !EsImporte(tokens[j]) && !EsPorcentaje(tokens[j]))
                {
                    j++;
                }

                var cola = tokens.Skip(j).ToArray();
                if (cola.Length < 2 || !cola.All(t => EsImporte(t) || EsPorcentaje(t)) || !EsImporte(cola[0]))
                {
                    continue;
                }

                var cantidad = ParsearImporte(tokens[i]);
                var precio = ParsearImporte(cola[0]);
                if (cantidad <= 0)
                {
                    return false;
                }

                // Sin código del proveedor, la descripción arranca en el primer token.
                var conCodigo = PareceCodigo(tokens[0]) && i > 1;
                renglon.Codigo = conCodigo ? tokens[0] : string.Empty;
                renglon.Descripcion = string.Join(' ', tokens.Skip(conCodigo ? 1 : 0).Take(i - (conCodigo ? 1 : 0)));
                renglon.Cantidad = cantidad;
                renglon.Unidad = string.Join(' ', tokens.Skip(i + 1).Take(j - i - 1));
                renglon.PrecioUnitario = precio;
                renglon.Subtotal = cola.Where(EsImporte).Select(ParsearImporte).Last();
                return renglon.Descripcion.Length > 0;
            }

            return false;
        }

        /// <summary>Acepta «1.234,56» (formato argentino), «1234,56» y «1234.56».</summary>
        public static decimal ParsearImporte(string texto)
        {
            var t = texto.Trim();
            if (t.Contains(','))
            {
                t = t.Replace(".", string.Empty).Replace(',', '.');
            }
            else if (Regex.IsMatch(t, @"^\d{1,3}(\.\d{3})+$"))
            {
                t = t.Replace(".", string.Empty);
            }

            return decimal.Parse(t, NumberStyles.Number, CultureInfo.InvariantCulture);
        }

        private static bool EsImporte(string token) => Importe.IsMatch(token);

        private static bool EsPorcentaje(string token) => Porcentaje.IsMatch(token);

        // Un código de proveedor es una sola palabra con algún dígito o guion (ej. «CER-500», «A123»).
        private static bool PareceCodigo(string token) => token.Any(char.IsDigit) || token.Contains('-');

        private static string SoloDigitos(string texto) => new(texto.Where(char.IsDigit).ToArray());
    }
}
