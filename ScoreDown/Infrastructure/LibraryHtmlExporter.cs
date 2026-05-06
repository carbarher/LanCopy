using ScoreDown.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScoreDown.Infrastructure;

public static class LibraryHtmlExporter
{
  private static string EscHtml(string s) =>
      s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#x27;");

  private static string SourceBadge(string source) => source switch
  {
    "IMSLP" => "<span class='badge badge-imslp'>IMSLP</span>",
    "CPDL" => "<span class='badge badge-cpdl'>CPDL</span>",
    "Mutopia" => "<span class='badge badge-mutopia'>Mutopia</span>",
    _ => $"<span class='badge badge-other'>{EscHtml(source)}</span>",
  };

  private static string FormatIcon(string fmt) => fmt switch
  {
    "PDF" => "📄",
    "MIDI" => "🎹",
    "XML" => "🗒",
    "MXL" => "🗒",
    _ => "📎",
  };

  private static string NormalizeMeta(string? value) =>
    string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

  private static string SafeMeta(string? value, string fallback) =>
    string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

  private static string InferGenre(string title)
  {
    var t = title.ToLowerInvariant();
    if (t.Contains("mass") || t.Contains("misa") || t.Contains("requiem")) return "Misa/Requiem";
    if (t.Contains("motet") || t.Contains("motete")) return "Motete";
    if (t.Contains("hymn") || t.Contains("himno") || t.Contains("chorale") || t.Contains("coral")) return "Himno/Coral";
    if (t.Contains("sonata")) return "Sonata";
    if (t.Contains("symphony") || t.Contains("sinfon")) return "Sinfonia";
    if (t.Contains("concerto") || t.Contains("concierto")) return "Concierto";
    if (t.Contains("prelude") || t.Contains("prelud")) return "Preludio";
    if (t.Contains("fugue") || t.Contains("fuga")) return "Fuga";
    if (t.Contains("waltz") || t.Contains("vals")) return "Vals";
    if (t.Contains("dance") || t.Contains("danza")) return "Danza";
    return "Sin genero";
  }

  private static string InferInstrument(string title)
  {
    var t = title.ToLowerInvariant();
    if (t.Contains("piano") || t.Contains("klavier") || t.Contains("pianoforte")) return "Piano";
    if (t.Contains("organ") || t.Contains("organo")) return "Organo";
    if (t.Contains("violoncello") || t.Contains("cello")) return "Cello";
    if (t.Contains("violin") || t.Contains("viol")) return "Violin";
    if (t.Contains("flute") || t.Contains("flauta")) return "Flauta";
    if (t.Contains("guitar") || t.Contains("guitarra")) return "Guitarra";
    if (t.Contains("choir") || t.Contains("chorus") || t.Contains("coro") || t.Contains("choral")) return "Coro";
    return "Sin instrumento";
  }

  public static string GenerateHtml(List<PartituraItem> items, string destFolder)
  {
    var grouped = items
        .GroupBy(i => string.IsNullOrWhiteSpace(i.Composer) ? "Varios" : i.Composer)
        .OrderBy(g => g.Key)
        .ToList();

    int totalFiles = items.Sum(i => i.Files.Count);
    int totalPdf = items.Sum(i => i.Files.Count(f => f.Format == "PDF"));
    int totalMidi = items.Sum(i => i.Files.Count(f => f.Format == "MIDI"));
    int totalXml = items.Sum(i => i.Files.Count(f => f.Format is "XML" or "MXL"));

    // per-source counts
    var bySrc = items.GroupBy(i => i.Source)
                     .ToDictionary(g => g.Key, g => g.Count());

    var authors = items
      .Select(i => SafeMeta(i.Composer, "Varios"))
      .Distinct(System.StringComparer.OrdinalIgnoreCase)
      .OrderBy(x => x)
      .ToList();

    var genres = items
      .Select(i => SafeMeta(NormalizeMeta(i.Genre), InferGenre(i.Title)))
      .Distinct(System.StringComparer.OrdinalIgnoreCase)
      .OrderBy(x => x)
      .ToList();

    var instruments = items
      .Select(i => SafeMeta(NormalizeMeta(i.Instrument), InferInstrument(i.Title)))
      .Distinct(System.StringComparer.OrdinalIgnoreCase)
      .OrderBy(x => x)
      .ToList();

    var sb = new StringBuilder();
    sb.Append("""
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>Biblioteca de Partituras</title>
  <style>
    :root {
      --bg: #f0f2f5;
      --card: #ffffff;
      --accent: #1a6fdb;
      --accent2: #0e4fa8;
      --text: #1e1e2e;
      --sub: #555;
      --border: #e2e6ea;
      --radius: 10px;
      --shadow: 0 2px 8px rgba(0,0,0,.10);
    }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body { font-family: "Segoe UI", system-ui, sans-serif; background: var(--bg); color: var(--text); }

    /* ── Header ─────────────────────────────── */
    header {
      background: linear-gradient(135deg, #0e4fa8 0%, #1a6fdb 100%);
      color: #fff; padding: 1.6rem 2rem 1.2rem;
      display: flex; align-items: flex-end; gap: 1.5rem; flex-wrap: wrap;
    }
    header h1 { font-size: 1.7rem; font-weight: 700; }
    header .meta { font-size: .82rem; opacity: .75; margin-bottom: .15rem; }

    /* ── Stats bar ───────────────────────────── */
    .stats-bar {
      background: var(--card); border-bottom: 1px solid var(--border);
      padding: .75rem 2rem; display: flex; gap: 1.5rem; flex-wrap: wrap; font-size: .9rem;
    }
    .stat { display: flex; flex-direction: column; align-items: center; }
    .stat .n { font-size: 1.4rem; font-weight: 700; color: var(--accent); line-height: 1; }
    .stat .l { color: var(--sub); font-size: .76rem; margin-top: .1rem; }

    /* ── Filters ─────────────────────────────── */
    .filters {
      padding: 1rem 2rem; position: sticky; top: 0; z-index: 10;
      background: var(--bg); border-bottom: 1px solid var(--border);
      display: grid; grid-template-columns: 1.2fr 1fr 1fr 1fr auto; gap: .55rem;
      align-items: center;
    }
    .filter-input, .filter-select {
      width: 100%; padding: .5rem .75rem;
      border: 1.5px solid var(--border); border-radius: 8px;
      font-size: .9rem; outline: none; background: var(--card);
      transition: border-color .15s;
    }
    .filter-input:focus, .filter-select:focus { border-color: var(--accent); }
    .reset-btn {
      border: 1px solid var(--border); background: var(--card); color: var(--text);
      border-radius: 8px; padding: .48rem .75rem; cursor: pointer; font-size: .85rem;
    }
    .reset-btn:hover { border-color: var(--accent); color: var(--accent); }

    /* ── Main layout ─────────────────────────── */
    main { padding: 1.2rem 2rem 3rem; max-width: 1080px; margin: 0 auto; }

    /* ── Composer card ───────────────────────── */
    .composer-card {
      background: var(--card); border-radius: var(--radius);
      box-shadow: var(--shadow); margin-bottom: 1rem; overflow: hidden;
    }
    .composer-header {
      display: flex; align-items: center; gap: .7rem;
      padding: .85rem 1.2rem; cursor: pointer;
      user-select: none; border-bottom: 1px solid var(--border);
      transition: background .12s;
    }
    .composer-header:hover { background: #f7f9fc; }
    .composer-name { font-weight: 600; font-size: 1.05rem; flex: 1; }
    .composer-meta { color: var(--sub); font-size: .82rem; white-space: nowrap; }
    .chevron { font-size: .8rem; transition: transform .2s; color: var(--sub); }
    .composer-card.open .chevron { transform: rotate(90deg); }
    .folder-btn {
      font-size: .78rem; background: #e8f0fe; color: var(--accent);
      border: none; border-radius: 6px; padding: .22rem .55rem; cursor: pointer;
      text-decoration: none; white-space: nowrap;
    }
    .folder-btn:hover { background: var(--accent); color: #fff; }

    /* ── Works list ──────────────────────────── */
    .works-list { display: none; }
    .composer-card.open .works-list { display: block; }

    .work-row {
      display: flex; align-items: baseline; gap: .6rem;
      padding: .52rem 1.2rem; border-bottom: 1px solid var(--border);
      font-size: .9rem;
    }
    .work-row:last-child { border-bottom: none; }
    .work-title { flex: 1; font-weight: 500; }
    .work-meta { display: inline-flex; gap: .3rem; flex-wrap: wrap; align-items: center; }
    .work-files { display: flex; gap: .35rem; flex-wrap: wrap; align-items: center; }

    .meta-chip {
      display: inline-block; border-radius: 5px; font-size: .72rem;
      padding: .12rem .42rem; background: #eef3ff; color: #2454a6;
      white-space: nowrap;
    }

    .file-chip {
      display: inline-flex; align-items: center; gap: .2rem;
      background: #f0f4ff; color: var(--accent2);
      border-radius: 5px; padding: .12rem .45rem;
      font-size: .76rem; text-decoration: none; white-space: nowrap;
    }
    .file-chip:hover { background: var(--accent); color: #fff; }
    .file-chip.missing { background: #fafafa; color: #aaa; cursor: default; }

    /* ── Badges ──────────────────────────────── */
    .badge {
      display: inline-block; border-radius: 4px; font-size: .68rem;
      font-weight: 700; padding: .1rem .38rem; letter-spacing: .03em;
    }
    .badge-imslp   { background: #d4edff; color: #0059b3; }
    .badge-cpdl    { background: #d4f5e4; color: #1a7a42; }
    .badge-mutopia { background: #fde8ff; color: #7a1aa8; }
    .badge-other   { background: #eee; color: #555; }

    /* ── No-results ──────────────────────────── */
    #no-results { display: none; text-align: center; color: var(--sub); padding: 3rem; }

    @media (max-width: 960px) {
      .filters { grid-template-columns: 1fr 1fr; }
    }
    @media (max-width: 640px) {
      .filters { grid-template-columns: 1fr; }
    }
  </style>
</head>
<body>
""");

    // header
    sb.Append($"""
<header>
  <div>
    <h1>📚 Biblioteca de Partituras</h1>
    <div class="meta">Generada: {System.DateTime.Now:dd/MM/yyyy HH:mm}</div>
  </div>
</header>
""");

    // stats bar
    sb.Append("<div class='stats-bar'>");
    sb.Append($"<div class='stat'><span class='n'>{items.Count}</span><span class='l'>Obras</span></div>");
    sb.Append($"<div class='stat'><span class='n'>{grouped.Count}</span><span class='l'>Compositores</span></div>");
    sb.Append($"<div class='stat'><span class='n'>{totalFiles}</span><span class='l'>Archivos</span></div>");
    sb.Append($"<div class='stat'><span class='n'>{totalPdf}</span><span class='l'>PDF</span></div>");
    sb.Append($"<div class='stat'><span class='n'>{totalMidi}</span><span class='l'>MIDI</span></div>");
    if (totalXml > 0)
      sb.Append($"<div class='stat'><span class='n'>{totalXml}</span><span class='l'>XML/MXL</span></div>");
    foreach (var kv in bySrc.OrderByDescending(x => x.Value))
      sb.Append($"<div class='stat'><span class='n'>{kv.Value}</span><span class='l'>{EscHtml(kv.Key)}</span></div>");
    sb.AppendLine("</div>");

    // filters
    sb.AppendLine("<div class='filters'>");
    sb.AppendLine("  <input id='search' class='filter-input' type='search' placeholder='Buscar obra...' autocomplete='off'>");

    sb.AppendLine("  <select id='filter-author' class='filter-select'>");
    sb.AppendLine("    <option value=''>Autor: todos</option>");
    foreach (var author in authors)
      sb.AppendLine($"    <option value='{EscHtml(author.ToLowerInvariant())}'>{EscHtml(author)}</option>");
    sb.AppendLine("  </select>");

    sb.AppendLine("  <select id='filter-genre' class='filter-select'>");
    sb.AppendLine("    <option value=''>Genero: todos</option>");
    foreach (var genre in genres)
      sb.AppendLine($"    <option value='{EscHtml(genre.ToLowerInvariant())}'>{EscHtml(genre)}</option>");
    sb.AppendLine("  </select>");

    sb.AppendLine("  <select id='filter-instrument' class='filter-select'>");
    sb.AppendLine("    <option value=''>Instrumento: todos</option>");
    foreach (var instrument in instruments)
      sb.AppendLine($"    <option value='{EscHtml(instrument.ToLowerInvariant())}'>{EscHtml(instrument)}</option>");
    sb.AppendLine("  </select>");

    sb.AppendLine("  <button id='reset-filters' class='reset-btn' type='button'>Limpiar</button>");
    sb.AppendLine("</div>");

    sb.AppendLine("<main>");
    sb.AppendLine("<div id='no-results'>Sin resultados para esa búsqueda.</div>");

    int composerIndex = 0;
    foreach (var composerGroup in grouped)
    {
      var composer = composerGroup.Key;
      var works = composerGroup.OrderBy(i => i.Title).ToList();
      var folderPath = Path.Combine(destFolder, composer);
      bool folderExists = Directory.Exists(folderPath);
      int cWorks = works.Count;
      int cFiles = works.Sum(w => w.Files.Count);
      int cPdf = works.Sum(w => w.Files.Count(f => f.Format == "PDF"));
      int cMidi = works.Sum(w => w.Files.Count(f => f.Format == "MIDI"));

      var srcSet = works.Select(w => w.Source).Distinct().OrderBy(s => s);
      var srcBadges = string.Join(" ", srcSet.Select(SourceBadge));

      // expand first composer by default
      string openClass = composerIndex == 0 ? " open" : "";
      composerIndex++;

      sb.AppendLine($"<div class='composer-card{openClass}' data-name='{EscHtml(composer.ToLowerInvariant())}'>");

      // header row
      sb.Append("  <div class='composer-header' onclick=\"this.closest('.composer-card').classList.toggle('open')\">");
      sb.Append("    <span class='chevron'>▶</span>");
      sb.Append($"   <span class='composer-name'>🎵 {EscHtml(composer)}</span>");
      sb.Append($"   {srcBadges}");
      sb.Append($"   <span class='composer-meta'>{cWorks} obras · {cFiles} archivos (PDF:{cPdf} MIDI:{cMidi})</span>");
      if (folderExists)
        sb.Append($"   <a class='folder-btn' href='{EscHtml("file:///" + folderPath.Replace("\\", "/"))}' onclick='event.stopPropagation()'>📂 Carpeta</a>");
      sb.AppendLine("  </div>");

      // works list
      sb.AppendLine("  <div class='works-list'>");
      foreach (var work in works)
      {
        var author = SafeMeta(work.Composer, "Varios");
        var genre = SafeMeta(NormalizeMeta(work.Genre), InferGenre(work.Title));
        var instrument = SafeMeta(NormalizeMeta(work.Instrument), InferInstrument(work.Title));

        sb.Append("    <div class='work-row' data-title='");
        sb.Append(EscHtml(work.Title.ToLowerInvariant()));
        sb.Append("' data-author='");
        sb.Append(EscHtml(author.ToLowerInvariant()));
        sb.Append("' data-genre='");
        sb.Append(EscHtml(genre.ToLowerInvariant()));
        sb.Append("' data-instrument='");
        sb.Append(EscHtml(instrument.ToLowerInvariant()));
        sb.Append("'>");
        sb.Append($"      <span class='work-title'>{EscHtml(work.Title)}</span>");
        sb.Append("      <span class='work-meta'>");
        sb.Append($"<span class='meta-chip' title='Genero'>Genero: {EscHtml(genre)}</span>");
        sb.Append($"<span class='meta-chip' title='Instrumento'>Instrumento: {EscHtml(instrument)}</span>");
        sb.Append($"<span class='meta-chip' title='Autor'>Autor: {EscHtml(author)}</span>");
        sb.Append("</span>");
        sb.Append("      <span class='work-files'>");

        foreach (var file in work.Files)
        {
          var filePath = Path.Combine(folderPath, file.FileName);
          bool exists = File.Exists(filePath);
          string icon = FormatIcon(file.Format);

          if (exists)
          {
            var uri = "file:///" + filePath.Replace("\\", "/");
            sb.Append($"<a class='file-chip' href='{EscHtml(uri)}' title='{EscHtml(file.FileName)}'>{icon} {EscHtml(file.Format)}</a>");
          }
          else
          {
            sb.Append($"<span class='file-chip missing' title='{EscHtml(file.FileName)}'>{icon} {EscHtml(file.Format)}</span>");
          }
        }
        sb.AppendLine("      </span>");
        sb.AppendLine("    </div>");
      }
      sb.AppendLine("  </div>");
      sb.AppendLine("</div>");
    }

    sb.Append("""
</main>
<script>
(function(){
  const input = document.getElementById('search');
  const authorSel = document.getElementById('filter-author');
  const genreSel = document.getElementById('filter-genre');
  const instrumentSel = document.getElementById('filter-instrument');
  const resetBtn = document.getElementById('reset-filters');
  const noRes = document.getElementById('no-results');
  const cards = document.querySelectorAll('.composer-card');

  function applyFilters() {
    const q = input.value.trim().toLowerCase();
    const author = authorSel.value;
    const genre = genreSel.value;
    const instrument = instrumentSel.value;
    const anyFilter = q || author || genre || instrument;
    let visibleCards = 0;

    // Colapsar todas las cards al limpiar filtros
    if (!anyFilter) cards.forEach(card => card.classList.remove('open'));

    cards.forEach(card => {
      const rows = card.querySelectorAll('.work-row');
      const cardName = (card.dataset.name || '');
      let anyVisible = false;

      rows.forEach(row => {
        const title = (row.dataset.title || '');
        const rowAuthor = (row.dataset.author || '');
        const rowGenre = (row.dataset.genre || '');
        const rowInstrument = (row.dataset.instrument || '');

        const matchTitle = !q || title.includes(q) || cardName.includes(q);
        const matchAuthor = !author || rowAuthor === author;
        const matchGenre = !genre || rowGenre === genre;
        const matchInstrument = !instrument || rowInstrument === instrument;
        const match = matchTitle && matchAuthor && matchGenre && matchInstrument;

        row.style.display = match ? '' : 'none';
        if (match) anyVisible = true;
      });

      card.style.display = anyVisible ? '' : 'none';
      if (anyVisible) {
        visibleCards++;
        if (anyFilter) card.classList.add('open');
      }
    });

    noRes.style.display = visibleCards === 0 ? 'block' : 'none';
  }

  input.addEventListener('input', applyFilters);
  authorSel.addEventListener('change', applyFilters);
  genreSel.addEventListener('change', applyFilters);
  instrumentSel.addEventListener('change', applyFilters);
  resetBtn.addEventListener('click', () => {
    input.value = '';
    authorSel.value = '';
    genreSel.value = '';
    instrumentSel.value = '';
    applyFilters();
  });
})();
</script>
</body>
</html>
""");

    return sb.ToString();
  }
}

