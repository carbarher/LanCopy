using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown.Tests
{
    /// <summary>
    /// Pruebas para el sistema de búsqueda multi-red
    /// </summary>
    public class MultiNetworkSearchTests
    {
        private NetworkOrchestrator orchestrator;
        private MockSearchProvider mockSoulseek;

        public void Setup()
        {
            orchestrator = new NetworkOrchestrator();
            mockSoulseek = new MockSearchProvider("Soulseek");
        }

        public async Task<TestResult> TestSingleNetworkSearch()
        {
            Setup();
            
            try
            {
                orchestrator.RegisterSearchProvider("Soulseek", mockSoulseek);
                
                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Fundacion.epub",
                    SizeBytes = 1024000,
                    Username = "user1",
                    NetworkSource = "Soulseek"
                });

                var request = new SearchRequest
                {
                    Query = "Isaac Asimov",
                    MaxResults = 100,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = await orchestrator.SearchAsync(request);

                if (response.DeduplicatedResults.Count != 1)
                {
                    return new TestResult { Success = false, Message = $"Esperado 1 resultado, obtenido {response.DeduplicatedResults.Count}" };
                }

                if (response.DeduplicatedResults[0].NetworkSource != "Soulseek")
                {
                    return new TestResult { Success = false, Message = $"Red incorrecta: {response.DeduplicatedResults[0].NetworkSource}" };
                }

                return new TestResult { Success = true, Message = "Búsqueda en red única exitosa" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<TestResult> TestMultiNetworkSearch()
        {
            Setup();
            
            try
            {
                orchestrator.RegisterSearchProvider("Soulseek", mockSoulseek);

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Fundacion.epub",
                    SizeBytes = 1024000,
                    Username = "user1",
                    NetworkSource = "Soulseek"
                });

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Imperio.epub",
                    SizeBytes = 2048000,
                    Username = "user2",
                    NetworkSource = "Soulseek"
                });

                var request = new SearchRequest
                {
                    Query = "Isaac Asimov",
                    MaxResults = 100,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = await orchestrator.SearchAsync(request);

                if (response.DeduplicatedResults.Count != 2)
                {
                    return new TestResult { Success = false, Message = $"Esperado 2 resultados, obtenido {response.DeduplicatedResults.Count}" };
                }

                var soulseekResults = response.DeduplicatedResults.Count(r => r.NetworkSource == "Soulseek");

                if (soulseekResults != 2)
                {
                    return new TestResult { Success = false, Message = $"Distribución incorrecta: Soulseek={soulseekResults}" };
                }

                return new TestResult { Success = true, Message = "Búsqueda multi-red exitosa" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<TestResult> TestDeduplication()
        {
            Setup();
            
            try
            {
                orchestrator.RegisterSearchProvider("Soulseek", mockSoulseek);

                var duplicateFile = "Isaac Asimov - Fundacion.epub";
                var size = 1024000L;

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = duplicateFile,
                    SizeBytes = size,
                    Username = "user1",
                    NetworkSource = "Soulseek",
                    FreeSlots = 2
                });

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = duplicateFile,
                    SizeBytes = size,
                    Username = "user2",
                    NetworkSource = "Soulseek",
                    FreeSlots = 1
                });

                var request = new SearchRequest
                {
                    Query = "Isaac Asimov",
                    MaxResults = 100,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = await orchestrator.SearchAsync(request);

                if (response.DeduplicatedResults.Count != 1)
                {
                    return new TestResult { Success = false, Message = $"Deduplicación falló: esperado 1 resultado, obtenido {response.DeduplicatedResults.Count}" };
                }

                var result = response.DeduplicatedResults[0];
                if (result.NetworkSource != "Soulseek")
                {
                    return new TestResult { Success = false, Message = $"Priorización incorrecta: seleccionó {result.NetworkSource} en lugar de Soulseek" };
                }

                if (!result.Metadata.ContainsKey("AlternativeSources"))
                {
                    return new TestResult { Success = false, Message = "Metadata de fuentes alternativas no encontrada" };
                }

                return new TestResult { Success = true, Message = "Deduplicación y priorización exitosa" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<TestResult> TestNetworkFiltering()
        {
            Setup();
            
            try
            {
                orchestrator.RegisterSearchProvider("Soulseek", mockSoulseek);

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Fundacion.epub",
                    SizeBytes = 1024000,
                    Username = "user1",
                    NetworkSource = "Soulseek"
                });

                var request = new SearchRequest
                {
                    Query = "Isaac Asimov",
                    MaxResults = 100,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response = await orchestrator.SearchAsync(request, new[] { "Soulseek" });

                if (response.DeduplicatedResults.Count != 1)
                {
                    return new TestResult { Success = false, Message = $"Filtrado falló: esperado 1 resultado, obtenido {response.DeduplicatedResults.Count}" };
                }

                if (response.DeduplicatedResults[0].NetworkSource != "Soulseek")
                {
                    return new TestResult { Success = false, Message = $"Red incorrecta: {response.DeduplicatedResults[0].NetworkSource}" };
                }

                return new TestResult { Success = true, Message = "Filtrado por red exitoso" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<TestResult> TestCaching()
        {
            Setup();
            
            try
            {
                orchestrator.RegisterSearchProvider("Soulseek", mockSoulseek);

                mockSoulseek.AddMockResult(new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Fundacion.epub",
                    SizeBytes = 1024000,
                    Username = "user1",
                    NetworkSource = "Soulseek"
                });

                var request = new SearchRequest
                {
                    Query = "Isaac Asimov",
                    MaxResults = 100,
                    Timeout = TimeSpan.FromSeconds(30)
                };

                var response1 = await orchestrator.SearchAsync(request);
                var searchCount1 = mockSoulseek.SearchCount;

                var response2 = await orchestrator.SearchAsync(request);
                var searchCount2 = mockSoulseek.SearchCount;

                if (searchCount2 != searchCount1)
                {
                    return new TestResult { Success = false, Message = $"Caché no funcionó: búsquedas={searchCount1} vs {searchCount2}" };
                }

                if (response1.DeduplicatedResults.Count != response2.DeduplicatedResults.Count)
                {
                    return new TestResult { Success = false, Message = "Resultados de caché diferentes" };
                }

                return new TestResult { Success = true, Message = "Caché funcionando correctamente" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Excepción: {ex.Message}" };
            }
        }

        public async Task<List<TestResult>> RunAllTests()
        {
            var results = new List<TestResult>();

            results.Add(await TestSingleNetworkSearch());
            results.Add(await TestMultiNetworkSearch());
            results.Add(await TestDeduplication());
            results.Add(await TestNetworkFiltering());
            results.Add(await TestCaching());

            return results;
        }
    }

    public class MockSearchProvider : ISearchProvider
    {
        private readonly List<SlskDown.Core.SearchResult> mockResults = new List<SlskDown.Core.SearchResult>();
        public int SearchCount { get; private set; }

        public string ProviderName { get; }
        public bool IsReady => true;

        public event EventHandler<SearchResultsReceivedEventArgs> ResultsReceived;
        public event EventHandler<SearchCompletedEventArgs> SearchCompleted;

        public MockSearchProvider(string name)
        {
            ProviderName = name;
        }

        public void AddMockResult(SlskDown.Core.SearchResult result)
        {
            mockResults.Add(result);
        }

        public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            SearchCount++;
            
            await Task.Delay(100, cancellationToken);

            var response = new SearchResponse
            {
                SearchId = request.SearchId,
                Status = SearchStatus.Completed,
                Results = new List<SlskDown.Core.SearchResult>(mockResults)
            };

            SearchCompleted?.Invoke(this, new SearchCompletedEventArgs
            {
                SearchId = request.SearchId,
                Status = SearchStatus.Completed,
                TotalResults = mockResults.Count
            });

            return response;
        }

        public Task CancelSearchAsync(string searchId)
        {
            return Task.CompletedTask;
        }
    }

    public class TestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
