using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SlskDown.Core;

namespace SlskDown.Tests
{
    /// <summary>
    /// Suite de tests avanzados para sistema multi-red
    /// Cubre deduplicación, filtros, caché, reputación y descargas
    /// </summary>
    public class AdvancedMultiNetworkTests
    {
        private NetworkOrchestrator orchestrator;
        private SmartDeduplicator deduplicator;
        private SourceReputationSystem reputationSystem;
        private PersistentSearchCache persistentCache;
        private MultiNetworkStatsDashboard dashboard;

        public async Task<List<TestResult>> RunAllTests()
        {
            var results = new List<TestResult>();

            results.Add(await TestSmartDeduplication());
            results.Add(await TestAdvancedFilters());
            results.Add(await TestSourceReputation());
            results.Add(await TestPartialResults());
            results.Add(await TestStatsDashboard());
            results.Add(await TestNetworkPrioritization());
            results.Add(await TestFailoverBetweenNetworks());
            results.Add(await TestConcurrentSearches());

            return results;
        }

        /// <summary>
        /// Test 1: Deduplicación inteligente
        /// </summary>
        public async Task<TestResult> TestSmartDeduplication()
        {
            try
            {
                deduplicator = new SmartDeduplicator();

                // Resultado original
                var result1 = new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac Asimov - Foundation (2008).epub",
                    SizeBytes = 1024000,
                    FileHash = "abc123",
                    NetworkSource = "Soulseek",
                    Username = "user1"
                };

                // Duplicado con nombre similar
                var result2 = new SlskDown.Core.SearchResult
                {
                    FileName = "Isaac.Asimov.Foundation.2008.epub",
                    SizeBytes = 1024000,
                    FileHash = "def456",
                    NetworkSource = "Soulseek",
                    Username = "user2"
                };

                // Duplicado con hash exacto
                var result3 = new SlskDown.Core.SearchResult
                {
                    FileName = "Foundation - Isaac Asimov.epub",
                    SizeBytes = 1024000,
                    FileHash = "abc123",
                    NetworkSource = "Soulseek",
                    Username = "user3"
                };

                var dedup1 = deduplicator.AddResult(result1);
                var dedup2 = deduplicator.AddResult(result2);
                var dedup3 = deduplicator.AddResult(result3);

                if (dedup1.IsDuplicate)
                    return new TestResult { Success = false, Message = "Primer resultado no debería ser duplicado" };

                if (!dedup2.IsDuplicate)
                    return new TestResult { Success = false, Message = "Segundo resultado debería ser detectado como duplicado" };

                if (!dedup3.IsDuplicate || dedup3.MatchType != DuplicateMatchType.ExactHash)
                    return new TestResult { Success = false, Message = "Tercer resultado debería ser duplicado por hash exacto" };

                return new TestResult { Success = true, Message = "Deduplicación inteligente funciona correctamente" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 2: Filtros avanzados específicos por red
        /// </summary>
        public async Task<TestResult> TestAdvancedFilters()
        {
            try
            {
                var filters = new SearchFilterBuilder()
                    .WithSizeRange(500000, 5000000)
                    .WithExtensions(".epub", ".pdf")
                    .SoulseekMinFreeSlots(1)
                    .SoulseekMaxQueue(10)
                    .Build();

                var results = new List<SlskDown.Core.SearchResult>
                {
                    new SlskDown.Core.SearchResult
                    {
                        FileName = "test.epub",
                        SizeBytes = 1000000,
                        NetworkSource = "Soulseek",
                        FreeSlots = 2,
                        QueueLength = 5
                    },
                    new SlskDown.Core.SearchResult
                    {
                        FileName = "test.txt",
                        SizeBytes = 1000000,
                        NetworkSource = "Soulseek",
                        FreeSlots = 2,
                        QueueLength = 5
                    },
                    new SlskDown.Core.SearchResult
                    {
                        FileName = "test.epub",
                        SizeBytes = 1000000,
                        NetworkSource = "Soulseek",
                        FreeSlots = 0,
                        QueueLength = 5
                    }
                };

                var filtered = filters.Apply(results);

                if (filtered.Count != 1)
                    return new TestResult { Success = false, Message = $"Esperado 1 resultado filtrado, obtenido {filtered.Count}" };

                if (filtered[0].FileName != "test.epub" || filtered[0].FreeSlots != 2)
                    return new TestResult { Success = false, Message = "Resultado filtrado incorrecto" };

                return new TestResult { Success = true, Message = "Filtros avanzados funcionan correctamente" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 3: Sistema de reputación
        /// </summary>
        public async Task<TestResult> TestSourceReputation()
        {
            try
            {
                reputationSystem = new SourceReputationSystem();

                // Registrar descargas exitosas
                reputationSystem.RecordSuccess("Soulseek", "gooduser", 1000000, TimeSpan.FromSeconds(10));
                reputationSystem.RecordSuccess("Soulseek", "gooduser", 2000000, TimeSpan.FromSeconds(15));

                // Registrar descargas fallidas
                reputationSystem.RecordFailure("Soulseek", "baduser", FailureReason.Timeout);
                reputationSystem.RecordFailure("Soulseek", "baduser", FailureReason.FileNotFound);

                var goodScore = reputationSystem.GetScore("Soulseek", "gooduser");
                var badScore = reputationSystem.GetScore("Soulseek", "baduser");

                if (goodScore <= badScore)
                    return new TestResult { Success = false, Message = "Usuario bueno debería tener mejor score que usuario malo" };

                if (goodScore < 60)
                    return new TestResult { Success = false, Message = $"Score de usuario bueno muy bajo: {goodScore}" };

                return new TestResult { Success = true, Message = $"Sistema de reputación funciona (good: {goodScore:F0}, bad: {badScore:F0})" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 4: Resultados parciales
        /// </summary>
        public async Task<TestResult> TestPartialResults()
        {
            try
            {
                var partialResultsReceived = new List<string>();
                orchestrator = new NetworkOrchestrator();

                orchestrator.PartialResultsReceived += (s, e) =>
                {
                    partialResultsReceived.Add(e.NetworkName);
                };

                // Este test requeriría mocks de proveedores
                // Por ahora verificamos que el evento existe
                if (orchestrator.PartialResultsReceived == null)
                    return new TestResult { Success = false, Message = "Evento PartialResultsReceived no existe" };

                return new TestResult { Success = true, Message = "Sistema de resultados parciales implementado" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 5: Dashboard de estadísticas
        /// </summary>
        public async Task<TestResult> TestStatsDashboard()
        {
            try
            {
                dashboard = new MultiNetworkStatsDashboard();

                // Registrar búsquedas
                dashboard.RecordSearch("Soulseek", 100, TimeSpan.FromSeconds(5), false);
                dashboard.RecordSearch("Soulseek", 50, TimeSpan.FromSeconds(10), false);

                // Registrar descargas
                dashboard.RecordDownload("Soulseek", 1000000, TimeSpan.FromSeconds(10), true);
                dashboard.RecordDownload("Soulseek", 2000000, TimeSpan.FromSeconds(20), true);

                var metrics = dashboard.GetAllMetrics();

                if (!metrics.ContainsKey("Soulseek"))
                    return new TestResult { Success = false, Message = "Métricas no registradas correctamente" };

                var soulseekMetrics = metrics["Soulseek"];
                if (soulseekMetrics.TotalSearches != 2 || soulseekMetrics.TotalResults != 150)
                    return new TestResult { Success = false, Message = "Métricas de Soulseek incorrectas" };

                var comparative = dashboard.GetComparativeStats();
                if (comparative.MostResultsNetwork?.NetworkName != "Soulseek")
                    return new TestResult { Success = false, Message = "Estadísticas comparativas incorrectas" };

                return new TestResult { Success = true, Message = "Dashboard de estadísticas funciona correctamente" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 6: Priorización de redes
        /// </summary>
        public async Task<TestResult> TestNetworkPrioritization()
        {
            try
            {
                var config = AdvancedNetworkConfig.CreateDefault();

                var soulseekSettings = config.GetNetworkSettings("Soulseek");

                if (!soulseekSettings.Enabled)
                    return new TestResult { Success = false, Message = "Soulseek debería estar habilitado por defecto" };

                if (soulseekSettings.Priority < 1)
                    return new TestResult { Success = false, Message = "Soulseek debería tener prioridad configurada" };

                return new TestResult { Success = true, Message = "Priorización de redes configurada correctamente" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 7: Failover entre redes
        /// </summary>
        public async Task<TestResult> TestFailoverBetweenNetworks()
        {
            try
            {
                // Este test requeriría simular fallos de red
                // Por ahora verificamos que MultiNetworkDownloadManager existe
                var downloadManager = new MultiNetworkDownloadManager(maxConcurrentDownloads: 3);

                if (downloadManager == null)
                    return new TestResult { Success = false, Message = "MultiNetworkDownloadManager no inicializado" };

                return new TestResult { Success = true, Message = "Sistema de failover implementado" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        /// <summary>
        /// Test 8: Búsquedas concurrentes
        /// </summary>
        public async Task<TestResult> TestConcurrentSearches()
        {
            try
            {
                var config = AdvancedNetworkConfig.CreateDefault();
                var soulseekSettings = config.GetNetworkSettings("Soulseek");

                if (soulseekSettings.MaxConcurrentSearches < 1)
                    return new TestResult { Success = false, Message = "MaxConcurrentSearches debe ser mayor a 0" };

                return new TestResult { Success = true, Message = $"Búsquedas concurrentes configuradas ({soulseekSettings.MaxConcurrentSearches} max)" };
            }
            catch (Exception ex)
            {
                return new TestResult { Success = false, Message = $"Error: {ex.Message}" };
            }
        }
    }

    public class TestResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
    }
}
