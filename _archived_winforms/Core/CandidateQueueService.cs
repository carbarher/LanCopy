using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SlskDown.Core
{
    public sealed class CandidateQueueService : IDisposable
    {
        private readonly object sync = new();
        private readonly string persistencePath;
        private CandidateQueueState state = new();

        private readonly TimeSpan saveDebounce = TimeSpan.FromMilliseconds(500);
        private System.Threading.Timer? saveTimer;
        private bool saveScheduled;

        public CandidateQueueService(string persistencePath)
        {
            this.persistencePath = persistencePath ?? throw new ArgumentNullException(nameof(persistencePath));
        }

        public void Load()
        {
            lock (sync)
            {
                state = LoadStateUnsafe(persistencePath);
            }
        }

        public void Save()
        {
            lock (sync)
            {
                SaveStateUnsafe(persistencePath, state);
            }
        }

        public void ScheduleSave()
        {
            lock (sync)
            {
                saveTimer ??= new System.Threading.Timer(_ => FlushScheduledSave(), null, Timeout.Infinite, Timeout.Infinite);
                saveScheduled = true;
                saveTimer.Change(saveDebounce, Timeout.InfiniteTimeSpan);
            }
        }

        public void FlushPendingSaves()
        {
            FlushScheduledSave();
        }

        public void Dispose()
        {
            try
            {
                FlushScheduledSave();
            }
            catch
            {
            }

            lock (sync)
            {
                saveTimer?.Dispose();
                saveTimer = null;
            }
        }

        private void FlushScheduledSave()
        {
            try
            {
                lock (sync)
                {
                    if (!saveScheduled)
                    {
                        return;
                    }

                    saveScheduled = false;
                    SaveStateUnsafe(persistencePath, state);
                }
            }
            catch
            {
            }
        }

        public IReadOnlyList<CandidateQueueItem> GetSnapshot()
        {
            lock (sync)
            {
                return state.Items.Select(Clone).ToList();
            }
        }

        public IReadOnlyList<CandidateQueueItem> GetCandidates() =>
            GetByStatus(CandidateQueueItemStatus.Candidate);

        public IReadOnlyList<CandidateQueueItem> GetApproved() =>
            GetByStatus(CandidateQueueItemStatus.Approved);

        public bool AddOrUpdateCandidate(CandidateQueueItem item, out string? reason)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (item.File == null) throw new ArgumentException("Item.File no puede ser null", nameof(item));

            lock (sync)
            {
                var key = BuildDedupKey(item.File);
                var existing = state.Items.FirstOrDefault(i => BuildDedupKey(i.File) == key);
                if (existing != null)
                {
                    if (existing.Status == CandidateQueueItemStatus.Enqueued)
                    {
                        reason = "Ya estaba encolado";
                        return false;
                    }

                    existing.Score = item.Score;
                    existing.Reasons = item.Reasons ?? new List<string>();
                    existing.TargetAuthor = item.TargetAuthor;
                    existing.TargetTitle = item.TargetTitle;
                    existing.Status = CandidateQueueItemStatus.Candidate;
                    existing.LastUpdatedUtc = DateTime.UtcNow;
                    reason = "Actualizado";
                    return true;
                }

                item.Status = CandidateQueueItemStatus.Candidate;
                item.AddedAtUtc = DateTime.UtcNow;
                item.LastUpdatedUtc = DateTime.UtcNow;
                state.Items.Add(item);
                reason = "Agregado";
                return true;
            }
        }

        public int Approve(IEnumerable<string> ids)
        {
            if (ids == null) return 0;

            lock (sync)
            {
                var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int changed = 0;
                foreach (var item in state.Items)
                {
                    if (idSet.Contains(item.Id) && item.Status == CandidateQueueItemStatus.Candidate)
                    {
                        item.Status = CandidateQueueItemStatus.Approved;
                        item.LastUpdatedUtc = DateTime.UtcNow;
                        changed++;
                    }
                }

                return changed;
            }
        }

        public int Unapprove(IEnumerable<string> ids)
        {
            if (ids == null) return 0;

            lock (sync)
            {
                var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int changed = 0;
                foreach (var item in state.Items)
                {
                    if (idSet.Contains(item.Id) && item.Status == CandidateQueueItemStatus.Approved)
                    {
                        item.Status = CandidateQueueItemStatus.Candidate;
                        item.LastUpdatedUtc = DateTime.UtcNow;
                        changed++;
                    }
                }

                return changed;
            }
        }

        public int MarkEnqueued(IEnumerable<string> ids)
        {
            if (ids == null) return 0;

            lock (sync)
            {
                var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int changed = 0;
                foreach (var item in state.Items)
                {
                    if (idSet.Contains(item.Id) && item.Status == CandidateQueueItemStatus.Approved)
                    {
                        item.Status = CandidateQueueItemStatus.Enqueued;
                        item.LastUpdatedUtc = DateTime.UtcNow;
                        changed++;
                    }
                }

                return changed;
            }
        }

        public int Remove(IEnumerable<string> ids)
        {
            if (ids == null) return 0;

            lock (sync)
            {
                var idSet = ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                int before = state.Items.Count;
                state.Items.RemoveAll(item => idSet.Contains(item.Id));
                return before - state.Items.Count;
            }
        }

        private IReadOnlyList<CandidateQueueItem> GetByStatus(CandidateQueueItemStatus status)
        {
            lock (sync)
            {
                return state.Items
                    .Where(i => i.Status == status)
                    .Select(Clone)
                    .ToList();
            }
        }

        private static string BuildDedupKey(CandidateFileInfo file)
        {
            var name = (file?.FileName ?? string.Empty).Trim();
            var user = (file?.Username ?? string.Empty).Trim();
            var size = file?.SizeBytes ?? 0;
            return $"{name}|{user}|{size}".ToLowerInvariant();
        }

        private static CandidateQueueItem Clone(CandidateQueueItem item)
        {
            return new CandidateQueueItem
            {
                Id = item.Id,
                AddedAtUtc = item.AddedAtUtc,
                Status = item.Status,
                File = new CandidateFileInfo
                {
                    FileName = item.File.FileName,
                    Extension = item.File.Extension,
                    Username = item.File.Username,
                    FolderPath = item.File.FolderPath,
                    SizeBytes = item.File.SizeBytes,
                    UploadSpeed = item.File.UploadSpeed,
                    QueueLength = item.File.QueueLength,
                    FreeUploadSlots = item.File.FreeUploadSlots,
                    Author = item.File.Author,
                    Network = item.File.Network
                },
                Score = item.Score,
                Reasons = item.Reasons?.ToList() ?? new List<string>(),
                TargetAuthor = item.TargetAuthor,
                TargetTitle = item.TargetTitle,
                LastError = item.LastError,
                LastUpdatedUtc = item.LastUpdatedUtc
            };
        }

        private static CandidateQueueState LoadStateUnsafe(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return new CandidateQueueState();
                }

                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<CandidateQueueState>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return loaded ?? new CandidateQueueState();
            }
            catch
            {
                return new CandidateQueueState();
            }
        }

        private static void SaveStateUnsafe(string path, CandidateQueueState current)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(current, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(path, json);
        }
    }
}
