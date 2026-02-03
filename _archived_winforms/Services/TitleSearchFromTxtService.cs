using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Soulseek;
using SlskDown.Models;

using IOFile = System.IO.File;

namespace SlskDown.Services
{
    public sealed class TitleSearchFromTxtService
    {
        public sealed record TitleSearchQuery(string Query, string DisplayGroup);

        public sealed record AcceptedFile(AutoSearchFileResult File, string MetadataKey);

        public sealed record TitleSearchStats(
            long ScannedResponses,
            long ScannedFiles,
            long AcceptedFiles,
            long SkippedBlacklistedUsers,
            long SkippedSizeZero,
            long SkippedNoExtension,
            long SkippedBelowMinSize,
            long SkippedNotDocument,
            long SkippedNotSpanish,
            long SkippedBloomDownloaded,
            long SkippedBloomProcessed,
            long SkippedCarbarher,
            long SkippedAlreadyDownloaded,
            long SkippedOnlyNewLocalExists,
            long SkippedGlobalDedupe);

        public static string NormalizeFileNameForDedupe(string fileName)
        {
            return DedupeKeyHelpers.NormalizeFileNameForDedupe(fileName);
        }

        public async Task<TitleSearchStats> RunAsync(
            IReadOnlyList<TitleSearchQuery> queries,
            SoulseekClient client,
            int parallelism,
            int minFileSizeKB,
            bool onlyNewFiles,
            bool enforceSpanish,
            bool useBloomFilter,
            int maxAcceptedPerQuery,
            int maxResponsesPerQueryNoHit,
            int maxResponsesPerQueryAfterHit,
            bool carbarherCacheLoaded,
            Func<bool> shouldContinue,
            Func<Task> waitForRateLimitAsync,
            Func<string, bool> isBlacklistedUser,
            Func<string, bool> isDocumentFile,
            Func<string, bool> isSpanishText,
            Func<string, bool> bloomContainsDownloaded,
            Func<string, bool> bloomContainsProcessed,
            Action<string> bloomInsertProcessed,
            Func<string, bool> carbarherContains,
            Func<string, bool> isAlreadyDownloaded,
            Func<string, string, string> getDownloadPath,
            Func<long, string> formatFileSize,
            Func<string, bool> tryAddGlobalDedupeKey,
            Action<IReadOnlyList<AcceptedFile>> onAcceptedBatch,
            Action<TitleSearchQuery, int> onQueryCompleted,
            Action<TitleSearchStats> statsSnapshotCallback,
            Action<int, int> progressCallback,
            CancellationToken cancellationToken)
        {
            if (queries == null || queries.Count == 0)
            {
                return new TitleSearchStats(
                    ScannedResponses: 0,
                    ScannedFiles: 0,
                    AcceptedFiles: 0,
                    SkippedBlacklistedUsers: 0,
                    SkippedSizeZero: 0,
                    SkippedNoExtension: 0,
                    SkippedBelowMinSize: 0,
                    SkippedNotDocument: 0,
                    SkippedNotSpanish: 0,
                    SkippedBloomDownloaded: 0,
                    SkippedBloomProcessed: 0,
                    SkippedCarbarher: 0,
                    SkippedAlreadyDownloaded: 0,
                    SkippedOnlyNewLocalExists: 0,
                    SkippedGlobalDedupe: 0);
            }

            long scannedResponses = 0;
            long scannedFiles = 0;
            long acceptedFiles = 0;

            long skippedBlacklistedUsers = 0;
            long skippedSizeZero = 0;
            long skippedNoExtension = 0;
            long skippedBelowMinSize = 0;
            long skippedNotDocument = 0;
            long skippedNotSpanish = 0;
            long skippedBloomDownloaded = 0;
            long skippedBloomProcessed = 0;
            long skippedCarbarher = 0;
            long skippedAlreadyDownloaded = 0;
            long skippedOnlyNewLocalExists = 0;
            long skippedGlobalDedupe = 0;

            int processedQueries = 0;

            var searchOptions = new SearchOptions(
                searchTimeout: 5000,
                maximumPeerQueueLength: 100,
                filterResponses: true,
                minimumResponseFileCount: 1,
                minimumPeerUploadSpeed: 0);

            using var semaphore = new SemaphoreSlim(parallelism, parallelism);

            var tasks = queries.Select(async query =>
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                var acceptedForQuery = 0;
                try
                {
                    if (!shouldContinue() || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await waitForRateLimitAsync().ConfigureAwait(false);

                    var results = await client.SearchAsync(
                        SearchQuery.FromText(query.Query),
                        options: searchOptions,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    Interlocked.Add(ref scannedResponses, results.Responses.Count);

                    var responseIndex = 0;

                    foreach (var response in results.Responses)
                    {
                        if (!shouldContinue() || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (maxAcceptedPerQuery > 0 && acceptedForQuery >= maxAcceptedPerQuery)
                        {
                            break;
                        }

                        var responseLimit = acceptedForQuery > 0
                            ? maxResponsesPerQueryAfterHit
                            : maxResponsesPerQueryNoHit;

                        if (responseLimit > 0 && responseIndex >= responseLimit)
                        {
                            break;
                        }

                        responseIndex++;

                        if (isBlacklistedUser(response.Username))
                        {
                            Interlocked.Increment(ref skippedBlacklistedUsers);
                            continue;
                        }

                        var acceptedBatch = new List<AcceptedFile>();

                        var reachedLimit = false;

                        foreach (var file in response.Files)
                        {
                            Interlocked.Increment(ref scannedFiles);

                            if (!shouldContinue() || cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            if (maxAcceptedPerQuery > 0 && acceptedForQuery >= maxAcceptedPerQuery)
                            {
                                reachedLimit = true;
                                break;
                            }

                            if (file.Size == 0)
                            {
                                Interlocked.Increment(ref skippedSizeZero);
                                continue;
                            }

                            var extension = Path.GetExtension(file.Filename);
                            if (string.IsNullOrEmpty(extension))
                            {
                                Interlocked.Increment(ref skippedNoExtension);
                                continue;
                            }

                            if (minFileSizeKB > 0 && file.Size < (minFileSizeKB * 1024L))
                            {
                                Interlocked.Increment(ref skippedBelowMinSize);
                                continue;
                            }

                            var fileName = Path.GetFileName(file.Filename ?? string.Empty);
                            var detectionText = Path.GetFileNameWithoutExtension(fileName);
                            if (string.IsNullOrWhiteSpace(detectionText))
                            {
                                detectionText = fileName;
                            }

                            bool isDocument = isDocumentFile(fileName);
                            if (!isDocument)
                            {
                                Interlocked.Increment(ref skippedNotDocument);
                                continue;
                            }

                            bool isSpanish = isSpanishText(detectionText);
                            if (enforceSpanish && !isSpanish)
                            {
                                Interlocked.Increment(ref skippedNotSpanish);
                                continue;
                            }

                            if (useBloomFilter)
                            {
                                if (bloomContainsDownloaded(fileName))
                                {
                                    Interlocked.Increment(ref skippedBloomDownloaded);
                                    continue;
                                }

                                if (bloomContainsProcessed(fileName))
                                {
                                    Interlocked.Increment(ref skippedBloomProcessed);
                                    continue;
                                }
                            }

                            if (carbarherCacheLoaded && carbarherContains(fileName))
                            {
                                Interlocked.Increment(ref skippedCarbarher);
                                continue;
                            }

                            if (isAlreadyDownloaded(fileName))
                            {
                                Interlocked.Increment(ref skippedAlreadyDownloaded);
                                continue;
                            }

                            var dedupeKey = DedupeKeyHelpers.BuildRemoteFileKey(fileName, file.Size, normalizeFileName: true);
                            if (!tryAddGlobalDedupeKey(dedupeKey))
                            {
                                Interlocked.Increment(ref skippedGlobalDedupe);
                                continue;
                            }

                            if (useBloomFilter)
                            {
                                bloomInsertProcessed(fileName);
                            }

                            var authorGroup = string.IsNullOrWhiteSpace(query.DisplayGroup)
                                ? "ObrasTXT"
                                : query.DisplayGroup;

                            var newFile = new AutoSearchFileResult
                            {
                                Author = authorGroup,
                                Username = response.Username,
                                FileName = Path.GetFileName(file.Filename),
                                Directory = Path.GetDirectoryName(file.Filename) ?? string.Empty,
                                SizeBytes = file.Size,
                                SizeReadable = formatFileSize(file.Size),
                                IsSpanish = isSpanish,
                                IsDocument = isDocument,
                                Timestamp = DateTime.Now
                            };

                            if (onlyNewFiles)
                            {
                                var localPath = getDownloadPath(newFile.FileName, newFile.Author);
                                if (IOFile.Exists(localPath))
                                {
                                    Interlocked.Increment(ref skippedOnlyNewLocalExists);
                                    continue;
                                }
                            }

                            var metadataKey = DedupeKeyHelpers.BuildRemotePathKey(file.Filename, file.Size);
                            acceptedBatch.Add(new AcceptedFile(newFile, metadataKey));
                            Interlocked.Increment(ref acceptedFiles);
                            acceptedForQuery++;

                            if (maxAcceptedPerQuery > 0 && acceptedForQuery >= maxAcceptedPerQuery)
                            {
                                reachedLimit = true;
                                break;
                            }
                        }

                        if (acceptedBatch.Count > 0)
                        {
                            onAcceptedBatch(acceptedBatch);
                        }

                        if (reachedLimit)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                    var current = Interlocked.Increment(ref processedQueries);
                    onQueryCompleted(query, acceptedForQuery);

                    statsSnapshotCallback(new TitleSearchStats(
                        ScannedResponses: Interlocked.Read(ref scannedResponses),
                        ScannedFiles: Interlocked.Read(ref scannedFiles),
                        AcceptedFiles: Interlocked.Read(ref acceptedFiles),
                        SkippedBlacklistedUsers: Interlocked.Read(ref skippedBlacklistedUsers),
                        SkippedSizeZero: Interlocked.Read(ref skippedSizeZero),
                        SkippedNoExtension: Interlocked.Read(ref skippedNoExtension),
                        SkippedBelowMinSize: Interlocked.Read(ref skippedBelowMinSize),
                        SkippedNotDocument: Interlocked.Read(ref skippedNotDocument),
                        SkippedNotSpanish: Interlocked.Read(ref skippedNotSpanish),
                        SkippedBloomDownloaded: Interlocked.Read(ref skippedBloomDownloaded),
                        SkippedBloomProcessed: Interlocked.Read(ref skippedBloomProcessed),
                        SkippedCarbarher: Interlocked.Read(ref skippedCarbarher),
                        SkippedAlreadyDownloaded: Interlocked.Read(ref skippedAlreadyDownloaded),
                        SkippedOnlyNewLocalExists: Interlocked.Read(ref skippedOnlyNewLocalExists),
                        SkippedGlobalDedupe: Interlocked.Read(ref skippedGlobalDedupe)));
                    progressCallback(current, queries.Count);
                }
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return new TitleSearchStats(
                ScannedResponses: scannedResponses,
                ScannedFiles: scannedFiles,
                AcceptedFiles: acceptedFiles,
                SkippedBlacklistedUsers: skippedBlacklistedUsers,
                SkippedSizeZero: skippedSizeZero,
                SkippedNoExtension: skippedNoExtension,
                SkippedBelowMinSize: skippedBelowMinSize,
                SkippedNotDocument: skippedNotDocument,
                SkippedNotSpanish: skippedNotSpanish,
                SkippedBloomDownloaded: skippedBloomDownloaded,
                SkippedBloomProcessed: skippedBloomProcessed,
                SkippedCarbarher: skippedCarbarher,
                SkippedAlreadyDownloaded: skippedAlreadyDownloaded,
                SkippedOnlyNewLocalExists: skippedOnlyNewLocalExists,
                SkippedGlobalDedupe: skippedGlobalDedupe);
        }
    }
}
