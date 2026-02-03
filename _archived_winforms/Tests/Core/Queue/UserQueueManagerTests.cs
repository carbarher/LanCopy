using System;
using System.Linq;
using Xunit;
using SlskDown.Core.Queue;

namespace SlskDown.Tests.Core.Queue
{
    public class UserQueueManagerTests
    {
        private readonly UserQueueManager queueManager;

        public UserQueueManagerTests()
        {
            queueManager = new UserQueueManager(defaultQueueLimit: 10);
        }

        [Fact]
        public void CanQueueTransfer_ReturnsTrueWhenSpaceAvailable()
        {
            // Arrange
            var username = "user1";

            // Act
            var canQueue = queueManager.CanQueueTransfer(username);

            // Assert
            Assert.True(canQueue);
        }

        [Fact]
        public void CanQueueTransfer_ReturnsFalseWhenQueueFull()
        {
            // Arrange
            var username = "user1";
            queueManager.UpdateUserQueueLimit(username, 2);
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);

            // Act
            var canQueue = queueManager.CanQueueTransfer(username);

            // Assert
            Assert.False(canQueue);
        }

        [Fact]
        public void IncrementQueueSize_IncreasesSize()
        {
            // Arrange
            var username = "user1";

            // Act
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);
            var size = queueManager.GetQueueSize(username);

            // Assert
            Assert.Equal(2, size);
        }

        [Fact]
        public void DecrementQueueSize_DecreasesSize()
        {
            // Arrange
            var username = "user1";
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);

            // Act
            queueManager.DecrementQueueSize(username);
            var size = queueManager.GetQueueSize(username);

            // Assert
            Assert.Equal(1, size);
        }

        [Fact]
        public void DecrementQueueSize_DoesNotGoBelowZero()
        {
            // Arrange
            var username = "user1";

            // Act
            queueManager.DecrementQueueSize(username);
            queueManager.DecrementQueueSize(username);
            var size = queueManager.GetQueueSize(username);

            // Assert
            Assert.Equal(0, size);
        }

        [Fact]
        public void UpdateUserQueueLimit_UpdatesLimit()
        {
            // Arrange
            var username = "user1";

            // Act
            queueManager.UpdateUserQueueLimit(username, 5);
            var limit = queueManager.GetQueueLimit(username);

            // Assert
            Assert.Equal(5, limit);
        }

        [Fact]
        public void GetAvailableQueueSpace_ReturnsCorrectValue()
        {
            // Arrange
            var username = "user1";
            queueManager.UpdateUserQueueLimit(username, 5);
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);

            // Act
            var available = queueManager.GetAvailableQueueSpace(username);

            // Assert
            Assert.Equal(3, available);
        }

        [Fact]
        public void IsQueueFull_ReturnsTrueWhenFull()
        {
            // Arrange
            var username = "user1";
            queueManager.UpdateUserQueueLimit(username, 2);
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);

            // Act
            var isFull = queueManager.IsQueueFull(username);

            // Assert
            Assert.True(isFull);
        }

        [Fact]
        public void IsQueueFull_ReturnsFalseWhenNotFull()
        {
            // Arrange
            var username = "user1";
            queueManager.UpdateUserQueueLimit(username, 5);
            queueManager.IncrementQueueSize(username);

            // Act
            var isFull = queueManager.IsQueueFull(username);

            // Assert
            Assert.False(isFull);
        }

        [Fact]
        public void ResetQueueSize_ResetsToZero()
        {
            // Arrange
            var username = "user1";
            queueManager.IncrementQueueSize(username);
            queueManager.IncrementQueueSize(username);

            // Act
            queueManager.ResetQueueSize(username);
            var size = queueManager.GetQueueSize(username);

            // Assert
            Assert.Equal(0, size);
        }

        [Fact]
        public void GetStatistics_ReturnsCorrectStats()
        {
            // Arrange
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user2");

            // Act
            var stats = queueManager.GetStatistics();

            // Assert
            Assert.Equal(2, stats.TotalUsers);
            Assert.Equal(3, stats.TotalQueuedTransfers);
            Assert.Equal(1.5, stats.AverageQueueSize);
        }

        [Fact]
        public void GetUsersWithFullQueue_ReturnsCorrectUsers()
        {
            // Arrange
            queueManager.UpdateUserQueueLimit("user1", 2);
            queueManager.UpdateUserQueueLimit("user2", 2);
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user2");

            // Act
            var fullUsers = queueManager.GetUsersWithFullQueue();

            // Assert
            Assert.Single(fullUsers);
            Assert.Contains("user1", fullUsers);
        }

        [Fact]
        public void GetUsersByAvailableSpace_ReturnsOrderedList()
        {
            // Arrange
            queueManager.UpdateUserQueueLimit("user1", 5);
            queueManager.UpdateUserQueueLimit("user2", 5);
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user2");
            queueManager.IncrementQueueSize("user2");

            // Act
            var users = queueManager.GetUsersByAvailableSpace();

            // Assert
            Assert.Equal("user1", users[0].Username);
            Assert.Equal(4, users[0].Available);
            Assert.Equal("user2", users[1].Username);
            Assert.Equal(3, users[1].Available);
        }

        [Fact]
        public void CleanupInactiveUsers_RemovesOldUsers()
        {
            // Arrange
            queueManager.IncrementQueueSize("user1");
            queueManager.UpdateUserQueueLimit("user1", 5);

            // Act
            System.Threading.Thread.Sleep(100);
            queueManager.CleanupInactiveUsers(TimeSpan.FromMilliseconds(50));

            var size = queueManager.GetQueueSize("user1");

            // Assert
            Assert.Equal(0, size);
        }

        [Fact]
        public void Clear_RemovesAllData()
        {
            // Arrange
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user2");

            // Act
            queueManager.Clear();
            var stats = queueManager.GetStatistics();

            // Assert
            Assert.Equal(0, stats.TotalUsers);
            Assert.Equal(0, stats.TotalQueuedTransfers);
        }

        [Fact]
        public void MultipleUsers_MaintainIndependentQueues()
        {
            // Arrange
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user1");
            queueManager.IncrementQueueSize("user2");

            // Act
            var size1 = queueManager.GetQueueSize("user1");
            var size2 = queueManager.GetQueueSize("user2");

            // Assert
            Assert.Equal(2, size1);
            Assert.Equal(1, size2);
        }

        [Fact]
        public void ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                var userId = $"user{i % 10}";
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    queueManager.IncrementQueueSize(userId);
                    queueManager.DecrementQueueSize(userId);
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert - No debería lanzar excepciones
            var stats = queueManager.GetStatistics();
            Assert.True(stats.TotalUsers >= 0);
        }
    }
}
