using System;
using System.Threading.Tasks;
using Xunit;
using SlskDown.Core;
using SlskDown.Models;

namespace SlskDown.Tests
{
    public class DownloadManagerTests
    {
        [Fact]
        public void Constructor_WithNullConfig_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DownloadManager(null));
        }
        
        [Fact]
        public void AddToQueue_WithValidTask_AddsSuccessfully()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            var task = CreateTestTask("test.mp3");
            
            // Act
            manager.AddToQueue(task);
            var queue = manager.GetQueueSnapshot();
            
            // Assert
            Assert.Single(queue);
            Assert.Equal("test.mp3", queue[0].File.FileName);
        }
        
        [Fact]
        public void AddToQueue_WithNullTask_ThrowsArgumentNullException()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => manager.AddToQueue(null));
        }
        
        [Fact]
        public void RemoveFromQueue_WithExistingTask_RemovesSuccessfully()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            var task = CreateTestTask("test.mp3");
            manager.AddToQueue(task);
            
            // Act
            manager.RemoveFromQueue(task);
            var queue = manager.GetQueueSnapshot();
            
            // Assert
            Assert.Empty(queue);
        }
        
        [Fact]
        public void GetDownloadingTasksCount_WithNoActiveDownloads_ReturnsZero()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            
            // Act
            var queue = manager.GetQueueSnapshot();
            int count = queue.FindAll(t => t.Status == DownloadStatus.Downloading).Count;
            
            // Assert
            Assert.Equal(0, count);
        }
        
        [Fact]
        public void GetDownloadingTasksCount_WithActiveDownloads_ReturnsCorrectCount()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            var task1 = CreateTestTask("test1.mp3");
            var task2 = CreateTestTask("test2.mp3");
            task1.Status = DownloadStatus.Downloading;
            task2.Status = DownloadStatus.Queued;
            manager.AddToQueue(task1);
            manager.AddToQueue(task2);
            
            // Act
            var queue = manager.GetQueueSnapshot();
            int count = queue.FindAll(t => t.Status == DownloadStatus.Downloading).Count;
            
            // Assert
            Assert.Equal(1, count);
        }
        
        [Fact]
        public void IsProviderBlacklisted_WithNonBlacklistedProvider_ReturnsFalse()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            
            // Act
            bool isBlacklisted = manager.IsProviderBlacklisted("testuser");
            
            // Assert
            Assert.False(isBlacklisted);
        }
        
        [Fact]
        public void RecordProviderFailure_WithMultipleFailures_BlacklistsProvider()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            string username = "testuser";
            
            // Act
            manager.RecordProviderFailure(username);
            manager.RecordProviderFailure(username);
            manager.RecordProviderFailure(username);
            
            // Assert
            Assert.True(manager.IsProviderBlacklisted(username));
        }
        
        [Fact]
        public void ClearBlacklist_AfterBlacklisting_RemovesAllEntries()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            manager.RecordProviderFailure("user1");
            manager.RecordProviderFailure("user1");
            manager.RecordProviderFailure("user1");
            
            // Act
            manager.ClearBlacklist();
            
            // Assert
            Assert.False(manager.IsProviderBlacklisted("user1"));
        }
        
        [Fact]
        public void GetBlacklistSnapshot_ReturnsCorrectData()
        {
            // Arrange
            var config = new DownloadManagerConfig();
            var manager = new DownloadManager(config);
            manager.RecordProviderFailure("user1");
            manager.RecordProviderFailure("user2");
            
            // Act
            var blacklist = manager.GetBlacklistSnapshot();
            
            // Assert
            Assert.Equal(2, blacklist.Count);
            Assert.True(blacklist.ContainsKey("user1"));
            Assert.True(blacklist.ContainsKey("user2"));
        }
        
        // Helper method
        private DownloadTask CreateTestTask(string fileName)
        {
            return new DownloadTask
            {
                File = new AutoSearchFileResult
                {
                    FileName = fileName,
                    Username = "testuser",
                    SizeBytes = 1024000
                },
                Status = DownloadStatus.Queued,
                RetryCount = 0
            };
        }
    }
}
