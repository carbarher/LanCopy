using System;
using System.Linq;
using Xunit;
using SlskDown.Core.Statistics;

namespace SlskDown.Tests.Core.Statistics
{
    public class TransferStatisticsTests
    {
        private readonly TransferStatistics stats;

        public TransferStatisticsTests()
        {
            stats = new TransferStatistics();
        }

        [Fact]
        public void RecordTransferStart_IncrementsCounters()
        {
            // Act
            stats.RecordTransferStart("user1", "Soulseek");
            stats.RecordTransferStart("user1", "Soulseek");

            var globalStats = stats.GetGlobalStats();

            // Assert
            Assert.Equal(2, globalStats.TotalTransfers);
        }

        [Fact]
        public void UpdateProgress_UpdatesBytesAndSpeed()
        {
            // Arrange
            var username = "user1";
            var provider = "Soulseek";

            // Act
            stats.UpdateProgress(username, provider, 1000, 0, 100.0);
            stats.UpdateProgress(username, provider, 2000, 1000, 150.0);

            var userStats = stats.GetUserStats(username);

            // Assert
            Assert.Equal(2000, userStats.TotalBytes);
            Assert.True(userStats.AverageSpeed > 0);
        }

        [Fact]
        public void RecordTransferSuccess_IncrementsSuccessCounter()
        {
            // Arrange
            var username = "user1";
            var provider = "Soulseek";

            // Act
            stats.RecordTransferStart(username, provider);
            stats.RecordTransferSuccess(username, provider, 1000000, TimeSpan.FromSeconds(10));

            var globalStats = stats.GetGlobalStats();

            // Assert
            Assert.Equal(1, globalStats.SuccessfulTransfers);
            Assert.Equal(1.0, globalStats.SuccessRate);
        }

        [Fact]
        public void RecordTransferFailure_IncrementsFailureCounter()
        {
            // Arrange
            var username = "user1";
            var provider = "Soulseek";

            // Act
            stats.RecordTransferStart(username, provider);
            stats.RecordTransferFailure(username, provider, "Connection timeout");

            var globalStats = stats.GetGlobalStats();

            // Assert
            Assert.Equal(1, globalStats.FailedTransfers);
        }

        [Fact]
        public void GetUserStats_ReturnsCorrectStats()
        {
            // Arrange
            var username = "user1";
            var provider = "Soulseek";

            // Act
            stats.RecordTransferStart(username, provider);
            stats.UpdateProgress(username, provider, 5000, 0, 500.0);
            stats.RecordTransferSuccess(username, provider, 5000, TimeSpan.FromSeconds(10));

            var userStats = stats.GetUserStats(username);

            // Assert
            Assert.Equal(username, userStats.Username);
            Assert.Equal(5000, userStats.TotalBytes);
            Assert.Equal(1, userStats.TotalTransfers);
            Assert.Equal(1, userStats.SuccessfulTransfers);
            Assert.Equal(1.0, userStats.SuccessRate);
        }

        [Fact]
        public void GetProviderStats_ReturnsCorrectStats()
        {
            // Arrange
            var provider = "Soulseek";

            // Act
            stats.RecordTransferStart("user1", provider);
            stats.RecordTransferStart("user2", provider);
            stats.RecordTransferSuccess("user1", provider, 1000, TimeSpan.FromSeconds(1));

            var providerStats = stats.GetProviderStats(provider);

            // Assert
            Assert.Equal(provider, providerStats.Provider);
            Assert.Equal(2, providerStats.TotalTransfers);
            Assert.Equal(1, providerStats.SuccessfulTransfers);
        }

        [Fact]
        public void GetTopUsersByBytes_ReturnsTopUsers()
        {
            // Arrange
            stats.UpdateProgress("user1", "Soulseek", 1000, 0, 100);
            stats.UpdateProgress("user2", "Soulseek", 5000, 0, 100);
            stats.UpdateProgress("user3", "Soulseek", 3000, 0, 100);

            // Act
            var topUsers = stats.GetTopUsersByBytes(2);

            // Assert
            Assert.Equal(2, topUsers.Count);
            Assert.Equal("user2", topUsers[0].Username);
            Assert.Equal("user3", topUsers[1].Username);
        }

        [Fact]
        public void GetTopUsersBySpeed_ReturnsTopUsers()
        {
            // Arrange
            stats.UpdateProgress("user1", "Soulseek", 1000, 0, 100);
            stats.UpdateProgress("user2", "Soulseek", 1000, 0, 500);
            stats.UpdateProgress("user3", "Soulseek", 1000, 0, 300);

            // Act
            var topUsers = stats.GetTopUsersBySpeed(2);

            // Assert
            Assert.Equal(2, topUsers.Count);
            Assert.Equal("user2", topUsers[0].Username);
            Assert.Equal("user3", topUsers[1].Username);
        }

        [Fact]
        public void UserStats_TracksFailureReasons()
        {
            // Arrange
            var username = "user1";

            // Act
            stats.RecordTransferFailure(username, "Soulseek", "Timeout");
            stats.RecordTransferFailure(username, "Soulseek", "Timeout");
            stats.RecordTransferFailure(username, "Soulseek", "User offline");

            var userStats = stats.GetUserStats(username);
            var reasons = userStats.GetFailureReasons();

            // Assert
            Assert.Equal(2, reasons["Timeout"]);
            Assert.Equal(1, reasons["User offline"]);
            Assert.Equal("Timeout", userStats.GetMostCommonFailureReason());
        }

        [Fact]
        public void Clear_ResetsAllStats()
        {
            // Arrange
            stats.RecordTransferStart("user1", "Soulseek");
            stats.UpdateProgress("user1", "Soulseek", 1000, 0, 100);

            // Act
            stats.Clear();
            var globalStats = stats.GetGlobalStats();

            // Assert
            Assert.Equal(0, globalStats.TotalTransfers);
            Assert.Equal(0, globalStats.TotalBytesTransferred);
        }

        [Fact]
        public void MultipleProviders_MaintainSeparateStats()
        {
            // Arrange
            stats.RecordTransferStart("user1", "Soulseek");
            stats.RecordTransferStart("user2", "eMule");

            // Act
            var soulseekStats = stats.GetProviderStats("Soulseek");
            var emuleStats = stats.GetProviderStats("eMule");

            // Assert
            Assert.Equal(1, soulseekStats.TotalTransfers);
            Assert.Equal(1, emuleStats.TotalTransfers);
        }

        [Fact]
        public void AverageSpeed_CalculatedCorrectly()
        {
            // Arrange
            var username = "user1";
            var provider = "Soulseek";

            // Act
            stats.UpdateProgress(username, provider, 1000, 0, 100);
            stats.UpdateProgress(username, provider, 2000, 1000, 200);
            stats.UpdateProgress(username, provider, 3000, 2000, 300);

            var userStats = stats.GetUserStats(username);

            // Assert
            Assert.Equal(200.0, userStats.AverageSpeed); // (100 + 200 + 300) / 3
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
                    stats.RecordTransferStart(userId, "Soulseek");
                    stats.UpdateProgress(userId, "Soulseek", 1000, 0, 100);
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            var globalStats = stats.GetGlobalStats();

            // Assert
            Assert.Equal(100, globalStats.TotalTransfers);
        }
    }
}
