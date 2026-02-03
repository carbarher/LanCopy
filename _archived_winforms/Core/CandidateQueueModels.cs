using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SlskDown.Core
{
    public static class CandidateQueueSchema
    {
        public const int CurrentVersion = 1;
    }

    public enum CandidateQueueItemStatus
    {
        Candidate = 0,
        Approved = 1,
        Enqueued = 2,
        Skipped = 3
    }

    public sealed class BookTarget
    {
        public string Author { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string Language { get; set; } = "es";
        public List<string> Keywords { get; set; } = new();
        public int Weight { get; set; } = 0;
        public int Priority { get; set; } = 0;
    }

    public sealed class CandidateFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string? Extension { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? FolderPath { get; set; }
        public long SizeBytes { get; set; }
        public int UploadSpeed { get; set; }
        public int QueueLength { get; set; }
        public int FreeUploadSlots { get; set; }
        public string? Author { get; set; }
        public string Network { get; set; } = "Soulseek";
    }

    public sealed class RankedCandidate
    {
        public CandidateFileInfo File { get; set; } = new();
        public double Score { get; set; }
        public double Confidence { get; set; }
        public List<string> Reasons { get; set; } = new();
    }

    public sealed class CandidateQueueItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
        public CandidateQueueItemStatus Status { get; set; } = CandidateQueueItemStatus.Candidate;

        public CandidateFileInfo File { get; set; } = new();
        public double Score { get; set; }
        public List<string> Reasons { get; set; } = new();

        public string? TargetAuthor { get; set; }
        public string? TargetTitle { get; set; }

        public string? LastError { get; set; }
        public DateTime? LastUpdatedUtc { get; set; }
    }

    public sealed class CandidateQueueState
    {
        public int SchemaVersion { get; set; } = CandidateQueueSchema.CurrentVersion;
        public List<CandidateQueueItem> Items { get; set; } = new();

        [JsonIgnore]
        public DateTime LoadedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
