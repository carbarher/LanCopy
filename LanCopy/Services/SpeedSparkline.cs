using System;
using System.Collections.Generic;
using System.Linq;

namespace LanCopy.Services;

/// <summary>
/// Render puro de una sparkline (mini-grafica de barras) a partir de una serie
/// de valores. Sin dependencias de UI (testeable).
/// </summary>
public static class SpeedSparkline
{
    private static readonly char[] Bars = { '\u2581', '\u2582', '\u2583', '\u2584', '\u2585', '\u2586', '\u2587', '\u2588' };

    public static string Render(IEnumerable<double> values)
    {
        var list = values as IReadOnlyList<double> ?? values.ToList();
        if (list.Count == 0) return "";
        var max = list.Max();
        if (max <= 0) return "";
        return string.Concat(list.Select(v => Bars[(int)Math.Min(7, v / max * 7)]));
    }
}