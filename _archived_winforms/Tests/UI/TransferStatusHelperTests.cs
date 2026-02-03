using System;
using Xunit;
using SlskDown.UI;
using SlskDown.Models;

namespace SlskDown.Tests.UI
{
    public class TransferStatusHelperTests
    {
        [Fact]
        public void GetUserFriendlyStatus_Queued_ReturnsCorrectMessage()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Queued,
                QueuePosition = 5
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("En cola", status);
            Assert.Contains("5", status);
        }

        [Fact]
        public void GetUserFriendlyStatus_Downloading_IncludesProgress()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Downloading,
                Progress = 45.5,
                Speed = 1024 * 1024 * 1.5, // 1.5 MB/s
                EstimatedTimeRemaining = TimeSpan.FromMinutes(5)
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("Descargando", status);
            Assert.Contains("45.5%", status);
            Assert.Contains("MB/s", status);
            Assert.Contains("5m", status);
        }

        [Fact]
        public void GetUserFriendlyStatus_Completed_ShowsCheckmark()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Completed
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("Completado", status);
            Assert.Contains("✓", status);
        }

        [Fact]
        public void GetUserFriendlyStatus_UserOffline_IndicatesRetry()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.UserOffline
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("desconectado", status);
            Assert.Contains("reintentará", status);
        }

        [Fact]
        public void GenerateTransferTooltip_IncludesAllRelevantInfo()
        {
            // Arrange
            var task = new DownloadTask
            {
                FileName = "test.epub",
                Username = "user123",
                Network = "Soulseek",
                FileSize = 1024 * 1024 * 10, // 10 MB
                CurrentByteOffset = 1024 * 1024 * 5, // 5 MB
                Progress = 50.0,
                Speed = 1024 * 500, // 500 KB/s
                RetryCount = 2,
                ErrorMessage = "Connection timeout",
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
                FilePath = @"C:\Downloads\test.epub"
            };

            // Act
            var tooltip = TransferStatusHelper.GenerateTransferTooltip(task);

            // Assert
            Assert.Contains("test.epub", tooltip);
            Assert.Contains("user123", tooltip);
            Assert.Contains("Soulseek", tooltip);
            Assert.Contains("50.0%", tooltip);
            Assert.Contains("KB/s", tooltip);
            Assert.Contains("Reintentos: 2", tooltip);
            Assert.Contains("Connection timeout", tooltip);
            Assert.Contains(@"C:\Downloads\test.epub", tooltip);
        }

        [Fact]
        public void GenerateTransferTooltip_NullTask_ReturnsEmpty()
        {
            // Act
            var tooltip = TransferStatusHelper.GenerateTransferTooltip(null);

            // Assert
            Assert.Empty(tooltip);
        }

        [Fact]
        public void GetStatusColor_Downloading_ReturnsGreen()
        {
            // Act
            var color = TransferStatusHelper.GetStatusColor(TransferStatus.Transferring);

            // Assert
            Assert.Equal(100, color.R);
            Assert.Equal(200, color.G);
            Assert.Equal(100, color.B);
        }

        [Fact]
        public void GetStatusColor_Finished_ReturnsBrightGreen()
        {
            // Act
            var color = TransferStatusHelper.GetStatusColor(TransferStatus.Finished);

            // Assert
            Assert.Equal(100, color.R);
            Assert.Equal(255, color.G);
            Assert.Equal(100, color.B);
        }

        [Fact]
        public void GetStatusColor_Failed_ReturnsRed()
        {
            // Act
            var color = TransferStatusHelper.GetStatusColor(TransferStatus.NetworkError);

            // Assert
            Assert.Equal(255, color.R);
            Assert.True(color.G < 200);
            Assert.True(color.B < 200);
        }

        [Fact]
        public void GetStatusColor_Queued_ReturnsBlue()
        {
            // Act
            var color = TransferStatusHelper.GetStatusColor(TransferStatus.Queued);

            // Assert
            Assert.True(color.B > 150);
        }

        [Fact]
        public void GetUserFriendlyStatus_WithRetryScheduled_ShowsCountdown()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Queued,
                IsScheduled = true,
                RetryAt = DateTime.UtcNow.AddMinutes(3),
                RetryCount = 1
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("Reintentando", status);
            Assert.Contains("intento", status);
        }

        [Fact]
        public void GetUserFriendlyStatus_QueueFull_ShowsUserFriendlyMessage()
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.QueueFull
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains("Cola", status);
            Assert.Contains("llena", status);
        }

        [Theory]
        [InlineData(1024.0, "1.00 KB/s")]
        [InlineData(1024.0 * 1024, "1.00 MB/s")]
        [InlineData(1024.0 * 500, "500.00 KB/s")]
        [InlineData(0, "0 KB/s")]
        public void FormatSpeed_FormatsCorrectly(double bytesPerSecond, string expected)
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Downloading,
                Speed = bytesPerSecond
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains(expected, status);
        }

        [Theory]
        [InlineData(30, "30s")]
        [InlineData(90, "2m")]
        [InlineData(3600, "1.0h")]
        public void FormatTimeSpan_FormatsCorrectly(int seconds, string expectedContains)
        {
            // Arrange
            var task = new DownloadTask
            {
                Status = DownloadStatus.Downloading,
                EstimatedTimeRemaining = TimeSpan.FromSeconds(seconds)
            };

            // Act
            var status = TransferStatusHelper.GetUserFriendlyStatus(task);

            // Assert
            Assert.Contains(expectedContains, status);
        }
    }
}
