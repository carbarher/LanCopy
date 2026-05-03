using ScoreDown.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ScoreDown.Infrastructure;

public static class LibraryHtmlExporter
{
    private static string EscHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

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

    /* ── Search ──────────────────────────────── */
    .search-bar { padding: 1rem 2rem; position: sticky; top: 0; z-index: 10;
                  background: var(--bg); border-bottom: 1px solid var(--border); }
    #search {
      width: 100%; max-width: 520px; padding: .55rem 1rem;
      border: 1.5px solid var(--border); border-radius: 999px;
      font-size: .95rem; outline: none; background: var(--card);
      transition: border-color .15s;
    }
    #search:focus { border-color: var(--accent); }

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
    .work-files { display: flex; gap: .35rem; flex-wrap: wrap; align-items: center; }

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

        // search
        sb.AppendLine("<div class='search-bar'><input id='search' type='search' placeholder='🔍  Buscar compositor o obra...' autocomplete='off'></div>");

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
                sb.Append($"   <a class='folder-btn' href='file:///{folderPath.Replace("\\", "/")}' onclick='event.stopPropagation()'>📂 Carpeta</a>");
            sb.AppendLine("  </div>");

            // works list
            sb.AppendLine("  <div class='works-list'>");
            foreach (var work in works)
            {
                sb.Append("    <div class='work-row' data-title='");
                sb.Append(EscHtml(work.Title.ToLowerInvariant()));
                sb.Append("'>");
                sb.Append($"      <span class='work-title'>{EscHtml(work.Title)}</span>");
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
  const input   = document.getElementById('search');
  const noRes   = document.getElementById('no-results');
  const cards   = document.querySelectorAll('.composer-card');

  input.addEventListener('input', () => {
    const q = input.value.trim().toLowerCase();
    let visible = 0;

    cards.forEach(card => {
      if (!q) {
        card.style.display = '';
        visible++;
        return;
      }
      const name = card.dataset.name || '';
      const rows = card.querySelectorAll('.work-row');
      let any = name.includes(q);

      rows.forEach(row => {
        const match = row.dataset.title?.includes(q) || name.includes(q);
        row.style.display = match ? '' : 'none';
        if (match) any = true;
      });

      card.style.display = any ? '' : 'none';
      if (any) {
        visible++;
        if (q) card.classList.add('open');
      }
    });

    noRes.style.display = visible === 0 ? 'block' : 'none';
  });
})();
</script>
</body>
</html>
""");

        return sb.ToString();
    }
}

