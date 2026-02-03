using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using SlskDown.Core;
using SlskDown.Models;

namespace SlskDown.Tests
{
    public class StatisticsManagerTests : IDisposable
    {
        private readonly string testHistoryPath;
        private readonly string testStatsPath;
        
        public StatisticsManagerTests()
        {
            testHistoryPath = Path.GetTempFileName();
            testStatsPath = Path.GetTempFileName();
        }
        
        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new StatisticsManager(null));
        }
        
        [Fact]
        public void RecordSearch_IncrementsTotalSearches()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // Act
            manager.RecordSearch(true, 10);
            var stats = manager.GetStatistics();
            
            // Assert
            Assert.Equal(1, stats.TotalSearches);
            Assert.Equal(1, stats.SuccessfulSearches);
            Assert.Equal(10, stats.TotalResultsFound);
        }
        
        [Fact]
        public void RecordDownload_IncrementsTotalDownloads()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // Act
            manager.RecordDownload(true, 1024000, TimeSpan.FromSeconds(10));
            var stats = manager.GetStatistics();
            
            // Assert
            Assert.Equal(1, stats.TotalDownloads);
            Assert.Equal(1, stats.SuccessfulDownloads);
            Assert.Equal(1024000, stats.TotalBytesDownloaded);
            Assert.True(stats.AverageDownloadSpeed > 0);
        }
        
        [Fact]
        public void AddToHistory_AddsDownloadToHistory()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            var now = DateTime.Now;
            var download = new DownloadHistoryRecord
            {
                FileName = "test.mp3",
                SizeBytes = 1024000,
                DownloadedAt = now,
                CompletedAt = now
            };
            
            // Act
            manager.AddToHistory(download);
            var history = manager.GetHistory();
            
            // Assert
            Assert.Single(history);
            Assert.Equal("test.mp3", history[0].FileName);
        }
        
        [Fact]
        public void IsInHistory_WithExistingFile_ReturnsTrue()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            var now = DateTime.Now;
            var download = new DownloadHistoryRecord
            {
                FileName = "test.mp3",
                SizeBytes = 1024000,
                DownloadedAt = now,
                CompletedAt = now
            };
            manager.AddToHistory(download);
            
            // Act
            bool exists = manager.IsInHistory("test.mp3", 1024000);
            
            // Assert
            Assert.True(exists);
        }
        
        [Fact]
        public void IsInHistory_WithNonExistingFile_ReturnsFalse()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // Act
            bool exists = manager.IsInHistory("nonexistent.mp3", 1024000);
            
            // Assert
            Assert.False(exists);
        }
        
        [Fact]
        public void ClearHistory_RemovesAllHistory()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            var now = DateTime.Now;
            manager.AddToHistory(new DownloadHistoryRecord { FileName = "test1.mp3", SizeBytes = 1024000, DownloadedAt = now, CompletedAt = now });
            manager.AddToHistory(new DownloadHistoryRecord { FileName = "test2.mp3", SizeBytes = 2048000, DownloadedAt = now, CompletedAt = now });
            
            // Act
            manager.ClearHistory();
            var history = manager.GetHistory();
            
            // Assert
            Assert.Empty(history);
        }
        
        [Fact]
        public void RecordProviderSuccess_UpdatesProviderStats()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // Act
            manager.RecordProviderSuccess("testuser", 1024000, TimeSpan.FromSeconds(10));
            var stats = manager.GetProviderStats("testuser");
            
            // Assert
            Assert.NotNull(stats);
            Assert.Equal(1, stats.TotalDownloads);
            Assert.Equal(1, stats.SuccessfulDownloads);
            Assert.Equal(1024000, stats.TotalBytesDownloaded);
            Assert.True(stats.AverageSpeed > 0);
        }
        
        [Fact]
        public void RecordProviderFailure_UpdatesProviderStats()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // Act
            manager.RecordProviderFailure("testuser");
            var stats = manager.GetProviderStats("testuser");
            
            // Assert
            Assert.NotNull(stats);
            Assert.Equal(1, stats.TotalDownloads);
            Assert.Equal(1, stats.FailedDownloads);
        }
        
        [Fact]
        public void GetTopProviders_ReturnsOrderedBySuccessRate()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            
            // User1: 100% success (2/2)
            manager.RecordProviderSuccess("user1", 1024000, TimeSpan.FromSeconds(10));
            manager.RecordProviderSuccess("user1", 1024000, TimeSpan.FromSeconds(10));
            
            // User2: 50% success (1/2)
            manager.RecordProviderSuccess("user2", 1024000, TimeSpan.FromSeconds(10));
            manager.RecordProviderFailure("user2");
            
            // Act
            var topProviders = manager.GetTopProviders(10);
            
            // Assert
            Assert.Equal(2, topProviders.Count);
            Assert.Equal("user1", topProviders[0].Username);
            Assert.Equal(100.0, topProviders[0].SuccessRate);
        }
        
        [Fact]
        public async Task SaveAndLoadHistory_PreservesData()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager1 = new StatisticsManager(config);
            var now = DateTime.Now;
            manager1.AddToHistory(new DownloadHistoryRecord { FileName = "test.mp3", SizeBytes = 1024000, DownloadedAt = now, CompletedAt = now });
            
            // Act
            await manager1.SaveHistoryAsync();
            
            var manager2 = new StatisticsManager(config);
            await manager2.LoadHistoryAsync();
            var history = manager2.GetHistory();
            
            // Assert
            Assert.Single(history);
            Assert.Equal("test.mp3", history[0].FileName);
        }
        
        [Fact]
        public void ResetStatistics_ClearsAllStats()
        {
            // Arrange
            var config = CreateTestConfig();
            var manager = new StatisticsManager(config);
            manager.RecordSearch(true, 10);
            manager.RecordDownload(true, 1024000, TimeSpan.FromSeconds(10));
            
            // Act
            manager.ResetStatistics();
            var stats = manager.GetStatistics();
            
            // Assert
            Assert.Equal(0, stats.TotalSearches);
            Assert.Equal(0, stats.TotalDownloads);
        }
        
        private StatisticsManagerConfig CreateTestConfig()
        {
            return new StatisticsManagerConfig
            {
                HistoryFilePath = testHistoryPath,
                ProviderStatsFilePath = testStatsPath,
                MaxHistoryItems = 1000
            };
        }
        
        public void Dispose()
        {
            if (File.Exists(testHistoryPath))
                File.Delete(testHistoryPath);
            
            if (File.Exists(testStatsPath))
                File.Delete(testStatsPath);
        }
    }
}
