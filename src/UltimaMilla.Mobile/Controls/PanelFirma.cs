using System.Globalization;
using System.Text;

namespace UltimaMilla.Mobile.Controls;

/// <summary>
/// Recuadro donde el receptor firma con el dedo (RF 20: "firma del receptor en pantalla").
/// Es un GraphicsView de MAUI: no necesita paquetes extra. Cada trazo es la lista de puntos
/// que toca el dedo desde que apoya hasta que levanta.
/// </summary>
public sealed class PanelFirma : GraphicsView
{
    private readonly DibujoFirma _dibujo = new();

    public PanelFirma()
    {
        Drawable = _dibujo;

        // Apoya el dedo: empieza un trazo nuevo.
        StartInteraction += (_, e) =>
        {
            if (e.Touches.Length == 0) return;
            _dibujo.Trazos.Add([e.Touches[0]]);
            Invalidate();
        };

        // Mueve el dedo: agrega puntos al trazo actual.
        DragInteraction += (_, e) =>
        {
            if (e.Touches.Length == 0 || _dibujo.Trazos.Count == 0) return;
            _dibujo.Trazos[^1].Add(e.Touches[0]);
            Invalidate();
        };
    }

    /// <summary>Sin ningún trazo de verdad (un toque suelto no cuenta como firma).</summary>
    public bool EstaVacia => _dibujo.Trazos.All(t => t.Count < 3);

    public void Limpiar()
    {
        _dibujo.Trazos.Clear();
        Invalidate();
    }

    /// <summary>
    /// La firma como imagen SVG (texto): ocupa pocos bytes, no pierde calidad y se puede
    /// mostrar en el navegador. Es lo que se guarda en el teléfono como prueba.
    /// </summary>
    public string ExportarSvg()
    {
        var c = CultureInfo.InvariantCulture;
        var ancho = Math.Max(1, Width);
        var alto = Math.Max(1, Height);

        var svg = new StringBuilder();
        svg.Append(c, $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {ancho:0} {alto:0}\" width=\"{ancho:0}\" height=\"{alto:0}\">");
        svg.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>");

        foreach (var trazo in _dibujo.Trazos.Where(t => t.Count > 1))
        {
            svg.Append("<path fill=\"none\" stroke=\"#14202E\" stroke-width=\"3\" stroke-linecap=\"round\" stroke-linejoin=\"round\" d=\"");
            svg.Append(c, $"M{trazo[0].X:0.#} {trazo[0].Y:0.#}");
            foreach (var punto in trazo.Skip(1))
                svg.Append(c, $" L{punto.X:0.#} {punto.Y:0.#}");
            svg.Append("\"/>");
        }

        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>Dibuja los trazos en pantalla.</summary>
    private sealed class DibujoFirma : IDrawable
    {
        public List<List<PointF>> Trazos { get; } = [];

        public void Draw(ICanvas canvas, RectF rectangulo)
        {
            canvas.StrokeColor = Color.FromArgb("#14202E");
            canvas.StrokeSize = 3;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;

            foreach (var trazo in Trazos.Where(t => t.Count > 1))
            {
                var camino = new PathF(trazo[0]);
                foreach (var punto in trazo.Skip(1))
                    camino.LineTo(punto);
                canvas.DrawPath(camino);
            }
        }
    }
}
