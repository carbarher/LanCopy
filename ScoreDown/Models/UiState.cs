namespace ScoreDown.Models;

public sealed class UiState
{
    public string? DestinationFolder { get; set; }
    public string? Source { get; set; }
    public string? FilterSource { get; set; }
    public bool? EnableMutopia { get; set; }
    public bool? EnableCpdl { get; set; }
    public bool? EnableMusopen { get; set; }
    public bool? AutoContinuePendingBatches { get; set; }
    public int? AutoBatchLimit { get; set; }
    public string? MusopenCookieHeader { get; set; }
    public string? MusopenUserAgent { get; set; }
}
