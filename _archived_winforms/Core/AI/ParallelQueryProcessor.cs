using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SlskDown.Core.AI
{
    public class QueryResult
    {
        public string Query { get; set; }
        public string Response { get; set; }
        public bool Success { get; set; }
        public double ResponseTimeMs { get; set; }
    }

    /// <summary>
    /// Procesador de consultas en paralelo para búsquedas masivas
    /// </summary>
    public class ParallelQueryProcessor
    {
        private readonly OllamaClient ollamaClient;
        private readonly int maxParallelQueries;

        public ParallelQueryProcessor(OllamaClient client, int maxParallel = 3)
        {
            ollamaClient = client;
            maxParallelQueries = maxParallel;
        }

        /// <summary>
        /// Procesa múltiples consultas en paralelo
        /// </summary>
        public async Task<List<QueryResult>> ProcessQueriesAsync(List<string> queries, string model)
        {
            var results = new List<QueryResult>();
            var semaphore = new System.Threading.SemaphoreSlim(maxParallelQueries);
            var tasks = new List<Task<QueryResult>>();

            foreach (var query in queries)
            {
                await semaphore.WaitAsync();

                var task = Task.Run(async () =>
                {
                    try
                    {
                        var startTime = DateTime.Now;
                        var response = await ollamaClient.GetCompletionAsync(query, model);
                        var elapsed = (DateTime.Now - startTime).TotalMilliseconds;

                        return new QueryResult
                        {
                            Query = query,
                            Response = response,
                            Success = !string.IsNullOrEmpty(response),
                            ResponseTimeMs = elapsed
                        };
                    }
                    catch (Exception ex)
                    {
                        return new QueryResult
                        {
                            Query = query,
                            Response = $"Error: {ex.Message}",
                            Success = false,
                            ResponseTimeMs = 0
                        };
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                tasks.Add(task);
            }

            results = (await Task.WhenAll(tasks)).ToList();
            return results;
        }

        /// <summary>
        /// Procesa consultas con prioridad (las más simples primero)
        /// </summary>
        public async Task<List<QueryResult>> ProcessQueriesWithPriorityAsync(List<string> queries, string model)
        {
            // Ordenar por complejidad (simples primero)
            var sortedQueries = queries
                .OrderBy(q => ModelSelector.DetermineComplexity(q))
                .ToList();

            return await ProcessQueriesAsync(sortedQueries, model);
        }

        /// <summary>
        /// Procesa consultas en lotes
        /// </summary>
        public async Task<List<QueryResult>> ProcessInBatchesAsync(List<string> queries, string model, int batchSize = 5)
        {
            var allResults = new List<QueryResult>();

            for (int i = 0; i < queries.Count; i += batchSize)
            {
                var batch = queries.Skip(i).Take(batchSize).ToList();
                var batchResults = await ProcessQueriesAsync(batch, model);
                allResults.AddRange(batchResults);

                // Pequeña pausa entre lotes para no saturar
                if (i + batchSize < queries.Count)
                    await Task.Delay(100);
            }

            return allResults;
        }
    }
}
