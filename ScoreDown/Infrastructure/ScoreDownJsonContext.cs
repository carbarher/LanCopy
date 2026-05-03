using ScoreDown.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ScoreDown.Infrastructure;

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(List<PartituraItem>))]
[JsonSerializable(typeof(List<DownloadHistoryItem>))]
[JsonSerializable(typeof(UiState))]
[JsonSerializable(typeof(LibraryData))]
internal partial class ScoreDownJsonContext : JsonSerializerContext
{
}
