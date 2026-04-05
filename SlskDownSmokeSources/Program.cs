using System.Diagnostics;
using SlskDownAvalonia.Services;

// Prueba en red real: CervantesVirtualService y ElejandriaService.
// Uso: dotnet run --project SlskDownSmokeSources/SlskDownSmokeSources.csproj

var sw = Stopwatch.StartNew();
var root = Path.Combine(Path.GetTempPath(), "slsk_smoke_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    Console.WriteLine("=== 1) CervantesVirtualService.SearchBooksAsync (conectividad + parse) ===");
    var cv = new CervantesVirtualService();
    cv.OnLog += m => Console.WriteLine($"  {m}");
    var cvBooks = await cv.SearchBooksAsync("Miguel de Cervantes", CancellationToken.None).ConfigureAwait(false);
    Console.WriteLine($"  → Resultados «Miguel de Cervantes»: {cvBooks.Count}");

    Console.WriteLine();
    Console.WriteLine("=== 2) ElejandriaService.DownloadByAuthorAsync (1 página; enlaces según HTML del sitio) ===");
    var elDir = Path.Combine(root, "elejandria");
    Directory.CreateDirectory(elDir);
    var el = new ElejandriaService();
    el.OnLog += m => Console.WriteLine($"  {m}");
    var elResult = await el.DownloadByAuthorAsync(
        "Benito Pérez Galdós",
        elDir,
        CancellationToken.None,
        maxPages: 1).ConfigureAwait(false);
    Console.WriteLine($"  → Downloaded={elResult.Downloaded}, Skipped={elResult.Skipped}, Failed={elResult.Failed}");

    sw.Stop();
    Console.WriteLine();
    Console.WriteLine($"Listo en {sw.Elapsed.TotalSeconds:F1}s (sin excepciones).");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}
finally
{
    try
    {
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
    catch
    {
        // best-effort
    }
}
