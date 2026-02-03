$f="MainForm.cs"
Copy-Item $f "$f.bak"
$c=[IO.File]::ReadAllText($f)
$c=$c -replace 'new TabPage\("[^"]*Busqueda"\)','new TabPage("Busqueda")'
$c=$c -replace 'new TabPage\("[^"]*Descargas"\)','new TabPage("Descargas")'
$c=$c -replace 'new TabPage\("[^"]*Configuracion"\)','new TabPage("Configuracion")'
$c=$c -replace 'new TabPage\("[^"]*Lista Negra"\)','new TabPage("Lista Negra")'
$c=$c -replace 'new TabPage\("[^"]*Autores"\)','new TabPage("Autores")'
$c=$c -replace 'new TabPage\("[^"]*Watchlist"\)','new TabPage("Watchlist")'
$c=$c -replace 'new TabPage\("[^"]*Automatico"\)','new TabPage("Automatico")'
$c=$c -replace 'new TabPage\("[^"]*Log"\)','new TabPage("Log")'
[IO.File]::WriteAllText($f,$c,[Text.UTF8Encoding]$true)
dotnet build SlskDown.csproj -c Release
& "bin\Release\net8.0-windows\SlskDown.exe"
