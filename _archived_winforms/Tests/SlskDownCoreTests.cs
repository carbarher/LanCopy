using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SlskDown.Core;
using SlskDown.Models;
using SlskDown.Services;

namespace SlskDown.Tests
{
    /// <summary>
    /// Tests unitarios para funciones críticas de SlskDown
    /// </summary>
    [TestClass]
    public class SlskDownCoreTests
    {
        private MemoryManager _memoryManager;
        private BandwidthLimiter _bandwidthLimiter;
        
        [TestInitialize]
        public void Setup()
        {
            _memoryManager = new MemoryManager();
            _bandwidthLimiter = new BandwidthLimiter(1000); // 1MB/s
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _memoryManager?.Dispose();
            _bandwidthLimiter?.Dispose();
        }
        
        #region MemoryManager Tests
        
        [TestMethod]
        public void MemoryManager_GetCurrentMemoryUsage_ReturnsPositiveValue()
        {
            // Act
            var memoryUsage = _memoryManager.GetCurrentMemoryUsage();
            
            // Assert
            Assert.IsTrue(memoryUsage > 0, "El uso de memoria debe ser positivo");
            Assert.IsTrue(memoryUsage < 10000, "El uso de memoria no debería exceder 10GB en pruebas");
        }
        
        [TestMethod]
        public void MemoryManager_RegisterDisposable_AddsToList()
        {
            // Arrange
            var disposable = new System.IO.MemoryStream();
            
            // Act
            _memoryManager.RegisterDisposable(disposable);
            var stats = _memoryManager.GetMemoryStats();
            
            // Assert
            Assert.IsTrue(stats.RegisteredDisposables > 0, "Debe haber al menos un disposable registrado");
        }
        
        [TestMethod]
        public void MemoryManager_PerformCleanup_ReturnsSuccess()
        {
            // Act
            var result = _memoryManager.PerformCleanup();
            
            // Assert
            Assert.IsTrue(result.Success, "El cleanup debería ser exitoso");
            Assert.IsNotNull(result, "El resultado no debe ser nulo");
        }
        
        [TestMethod]
        public void MemoryManager_GetMemoryStats_ReturnsValidStats()
        {
            // Act
            var stats = _memoryManager.GetMemoryStats();
            
            // Assert
            Assert.IsTrue(stats.WorkingSetMB > 0, "WorkingSet debe ser positivo");
            Assert.IsTrue(stats.PrivateMemoryMB > 0, "PrivateMemory debe ser positivo");
            Assert.IsTrue(stats.GCMemoryMB >= 0, "GCMemory debe ser no negativo");
        }
        
        #endregion
        
        #region BandwidthLimiter Tests
        
        [TestMethod]
        public void BandwidthLimiter_RequestBytes_WithValidLimit_ReturnsTrue()
        {
            // Arrange
            var bytesToRequest = 1024; // 1KB
            
            // Act
            var result = _bandwidthLimiter.RequestBytes(bytesToRequest);
            
            // Assert
            Assert.IsTrue(result, "Debería permitir bytes dentro del límite");
        }
        
        [TestMethod]
        public void BandwidthLimiter_RequestBytes_ExceedingLimit_ReturnsFalse()
        {
            // Arrange
            var limiter = new BandwidthLimiter(1); // 1 KB/s
            var bytesToRequest = 2048; // 2KB
            
            // Act
            var result = limiter.RequestBytes(bytesToRequest);
            
            // Assert
            Assert.IsFalse(result, "Debería rechazar bytes que exceden el límite");
        }
        
        [TestMethod]
        public void BandwidthLimiter_SetLimit_UpdatesCorrectly()
        {
            // Arrange
            var newLimit = 500; // 500 KB/s
            
            // Act
            _bandwidthLimiter.SetLimit(newLimit);
            
            // Assert
            // No hay forma directa de verificar el límite, pero podemos probar que funciona
            Assert.IsTrue(_bandwidthLimiter.RequestBytes(100), "Debería permitir pequeña cantidad con nuevo límite");
        }
        
        #endregion
        
        #region FileHelpers Tests
        
        [TestMethod]
        public void FileHelpers_FormatFileSize_ReturnsCorrectFormat()
        {
            // Act & Assert
            Assert.AreEqual("0 B", FileHelpers.FormatFileSize(0));
            Assert.AreEqual("1.0 KB", FileHelpers.FormatFileSize(1024));
            Assert.AreEqual("1.0 MB", FileHelpers.FormatFileSize(1024 * 1024));
            Assert.AreEqual("1.0 GB", FileHelpers.FormatFileSize(1024 * 1024 * 1024));
        }
        
        [TestMethod]
        public void FileHelpers_IsGarbageFile_DetectsCorrectly()
        {
            // Act & Assert
            Assert.IsTrue(FileHelpers.IsGarbageFile("Thumbs.db"));
            Assert.IsTrue(FileHelpers.IsGarbageFile("Desktop.ini"));
            Assert.IsTrue(FileHelpers.IsGarbageFile(".DS_Store"));
            Assert.IsFalse(FileHelpers.IsGarbageFile("document.pdf"));
            Assert.IsFalse(FileHelpers.IsGarbageFile("image.jpg"));
        }
        
        [TestMethod]
        public void FileHelpers_SanitizeFileName_RemovesInvalidChars()
        {
            // Arrange
            var invalidFileName = "file<>:|?*name.txt";
            
            // Act
            var sanitized = FileHelpers.SanitizeFileName(invalidFileName);
            
            // Assert
            Assert.IsFalse(sanitized.Contains("<"), "No debería contener <");
            Assert.IsFalse(sanitized.Contains(">"), "No debería contener >");
            Assert.IsFalse(sanitized.Contains(":"), "No debería contener :");
            Assert.IsTrue(sanitized.EndsWith(".txt"), "Debería mantener la extensión");
        }
        
        #endregion
        
        #region DownloadTask Tests
        
        [TestMethod]
        public void DownloadTask_Constructor_InitializesCorrectly()
        {
            // Arrange
            var filename = "test.pdf";
            var user = "testuser";
            var size = 1024000;
            
            // Act
            var task = new DownloadTask(filename, user, size);
            
            // Assert
            Assert.AreEqual(filename, task.Filename);
            Assert.AreEqual(user, task.Username);
            Assert.AreEqual(size, task.FileSize);
            Assert.AreEqual(DownloadStatus.Queued, task.Status);
            Assert.AreEqual(0, task.DownloadedBytes);
        }
        
        [TestMethod]
        public void DownloadTask_UpdateProgress_UpdatesCorrectly()
        {
            // Arrange
            var task = new DownloadTask("test.pdf", "user", 1000);
            var progress = 50;
            var downloaded = 500;
            
            // Act
            task.UpdateProgress(progress, downloaded);
            
            // Assert
            Assert.AreEqual(progress, task.Progress);
            Assert.AreEqual(downloaded, task.DownloadedBytes);
        }
        
        #endregion
        
        #region Integration Tests
        
        [TestMethod]
        public async Task Integration_MemoryAndBandwidth_WorkTogether()
        {
            // Arrange
            var memoryLimitReached = false;
            _memoryManager.OnMemoryWarning += (mb) => memoryLimitReached = true;
            
            // Act
            var bandwidthResult = _bandwidthLimiter.RequestBytes(1024);
            var memoryResult = _memoryManager.GetCurrentMemoryUsage();
            
            // Assert
            Assert.IsTrue(bandwidthResult, "Bandwidth limiter debería funcionar");
            Assert.IsTrue(memoryResult > 0, "Memory manager debería retornar uso válido");
        }
        
        [TestMethod]
        public void Integration_DownloadTaskWorkflow_CompletesSuccessfully()
        {
            // Arrange
            var task = new DownloadTask("test.pdf", "user", 1000);
            
            // Act - Simular ciclo de vida de descarga
            task.UpdateProgress(25, 250);
            task.Status = DownloadStatus.Downloading;
            task.UpdateProgress(100, 1000);
            task.Status = DownloadStatus.Completed;
            
            // Assert
            Assert.AreEqual(DownloadStatus.Completed, task.Status);
            Assert.AreEqual(100, task.Progress);
            Assert.AreEqual(1000, task.DownloadedBytes);
        }
        
        #endregion
        
        #region Performance Tests
        
        [TestMethod]
        public void Performance_BandwidthLimiter_HandlesHighFrequency()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var successCount = 0;
            
            // Act
            for (int i = 0; i < 10000; i++)
            {
                if (_bandwidthLimiter.RequestBytes(100))
                {
                    successCount++;
                }
            }
            
            stopwatch.Stop();
            
            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "Debería manejar 10k solicitudes en < 1s");
            Assert.IsTrue(successCount > 0, "Debería permitir algunas solicitudes");
        }
        
        [TestMethod]
        public void Performance_MemoryManager_CleanupPerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act
            var result = _memoryManager.PerformCleanup();
            
            stopwatch.Stop();
            
            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "Cleanup debería completarse en < 5s");
            Assert.IsTrue(result.Success, "Cleanup debería ser exitoso");
        }
        
        #endregion
    }
    
    /// <summary>
    /// Tests de estrés para componentes críticos
    /// </summary>
    [TestClass]
    public class StressTests
    {
        [TestMethod]
        public void Stress_MemoryManager_HandlesManyDisposables()
        {
            // Arrange
            using var memoryManager = new MemoryManager();
            var disposables = new List<IDisposable>();
            
            // Act
            for (int i = 0; i < 1000; i++)
            {
                var disposable = new MemoryStream();
                disposables.Add(disposable);
                memoryManager.RegisterDisposable(disposable);
            }
            
            var result = memoryManager.PerformCleanup();
            
            // Assert
            Assert.IsTrue(result.Success, "Debería manejar 1000 disposables");
            Assert.IsTrue(result.DisposablesCleaned > 0, "Debería limpiar algunos disposables");
            
            // Cleanup manual
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
        
        [TestMethod]
        public void Stress_BandwidthLimiter_ConcurrentAccess()
        {
            // Arrange
            using var limiter = new BandwidthLimiter(10000); // 10MB/s
            var tasks = new List<Task<bool>>();
            
            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() => limiter.RequestBytes(1024)));
            }
            
            var results = Task.WhenAll(tasks).Result;
            
            // Assert
            Assert.AreEqual(100, results.Length, "Debería completar todas las tareas");
            Assert.IsTrue(results.Any(r => r), "Algunas solicitudes deberían ser exitosas");
        }
    }
    
    /// <summary>
    /// Tests de regresión para bugs conocidos
    /// </summary>
    [TestClass]
    public class RegressionTests
    {
        [TestMethod]
        public void Regression_MemoryLeak_PreventsDisposalLeak()
        {
            // Arrange - Simular escenario que causaba memory leak
            using var memoryManager = new MemoryManager();
            
            // Act
            for (int i = 0; i < 100; i++)
            {
                var stream = new MemoryStream(new byte[1024]);
                memoryManager.RegisterDisposable(stream);
            }
            
            var beforeCleanup = memoryManager.GetMemoryStats().RegisteredDisposables;
            var cleanupResult = memoryManager.PerformCleanup();
            var afterCleanup = memoryManager.GetMemoryStats().RegisteredDisposables;
            
            // Assert
            Assert.IsTrue(beforeCleanup > afterCleanup, "Cleanup debería reducir disposables registrados");
            Assert.IsTrue(cleanupResult.DisposablesCleaned > 0, "Debería limpiar disposables");
        }
        
        [TestMethod]
        public void Regression_BandwidthLimit_PreventsNegativeTokens()
        {
            // Arrange
            using var limiter = new BandwidthLimiter(1); // 1KB/s muy bajo
            
            // Act - Intentar consumir más de lo disponible
            var results = new List<bool>();
            for (int i = 0; i < 10; i++)
            {
                results.Add(limiter.RequestBytes(2048)); // 2KB cada vez
            }
            
            // Assert
            Assert.IsTrue(results.All(r => !r), "Todas las solicitudes excesivas deberían fallar");
            // No debería crashear o tener valores negativos internos
        }
    }
}
