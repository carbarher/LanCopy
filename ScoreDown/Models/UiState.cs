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
    public bool? AutoConvertAudiveris { get; set; }
    public bool? AutoConvertOemer { get; set; }
    public bool? OnlyClassical { get; set; }
    public int? LastAudiverisBatchSize { get; set; }
    public int? LastOemerBatchSize { get; set; }
    public double? AudiverisFallbackBudgetScale { get; set; }
    public double? OemerFallbackBudgetScale { get; set; }
    public double? AudiverisParallelScale { get; set; }
    public double? OemerParallelScale { get; set; }
    public int? OemerTimeoutHeavyStreak { get; set; }
    public int? AudiverisTimeoutHeavyStreak { get; set; }
}
