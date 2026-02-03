using System;
using System.IO;
using System.Net.Sockets;
using Xunit;
using SlskDown.Models;

namespace SlskDown.Tests.Models
{
    public class TransferErrorTests
    {
        [Fact]
        public void FromException_TimeoutException_ClassifiesCorrectly()
        {
            // Arrange
            var ex = new TimeoutException("Connection timed out");

            // Act
            var error = TransferError.FromException(ex);

            // Assert
            Assert.Equal(TransferFailureReason.TimeoutError, error.Reason);
            Assert.True(error.IsRetryable);
            Assert.Equal(TimeSpan.FromMinutes(2), error.SuggestedRetryDelay);
        }

        [Fact]
        public void FromException_SocketException_ClassifiesCorrectly()
        {
            // Arrange
            var ex = new SocketException((int)SocketError.ConnectionRefused);

            // Act
            var error = TransferError.FromException(ex);

            // Assert
            Assert.Equal(TransferFailureReason.ConnectionRefused, error.Reason);
            Assert.True(error.IsRetryable);
        }

        [Fact]
        public void FromException_IOException_DiskFull_ClassifiesCorrectly()
        {
            // Arrange
            var ex = new IOException("Disk is full");

            // Act
            var error = TransferError.FromException(ex);

            // Assert
            Assert.Equal(TransferFailureReason.DiskFull, error.Reason);
            Assert.False(error.IsRetryable);
        }

        [Fact]
        public void FromException_UnauthorizedAccessException_ClassifiesCorrectly()
        {
            // Arrange
            var ex = new UnauthorizedAccessException("Access denied");

            // Act
            var error = TransferError.FromException(ex);

            // Assert
            Assert.Equal(TransferFailureReason.PermissionDenied, error.Reason);
            Assert.False(error.IsRetryable);
        }

        [Fact]
        public void FromException_OperationCanceledException_ClassifiesCorrectly()
        {
            // Arrange
            var ex = new OperationCanceledException("User cancelled");

            // Act
            var error = TransferError.FromException(ex);

            // Assert
            Assert.Equal(TransferFailureReason.UserCancelled, error.Reason);
            Assert.False(error.IsRetryable);
        }

        [Fact]
        public void FromSoulseekRejection_Banned_ClassifiesCorrectly()
        {
            // Arrange
            var message = "You are banned from downloading";

            // Act
            var error = TransferError.FromSoulseekRejection(message);

            // Assert
            Assert.Equal(TransferFailureReason.UserBanned, error.Reason);
            Assert.False(error.IsRetryable);
        }

        [Fact]
        public void FromSoulseekRejection_QueueFull_ClassifiesCorrectly()
        {
            // Arrange
            var message = "Queue is full";

            // Act
            var error = TransferError.FromSoulseekRejection(message);

            // Assert
            Assert.Equal(TransferFailureReason.QueueFull, error.Reason);
            Assert.True(error.IsRetryable);
            Assert.Equal(TimeSpan.FromMinutes(10), error.SuggestedRetryDelay);
        }

        [Fact]
        public void FromSoulseekRejection_FileNotAvailable_ClassifiesCorrectly()
        {
            // Arrange
            var message = "File not available";

            // Act
            var error = TransferError.FromSoulseekRejection(message);

            // Assert
            Assert.Equal(TransferFailureReason.FileNotAvailable, error.Reason);
            Assert.False(error.IsRetryable);
        }

        [Fact]
        public void FromSoulseekRejection_UserBusy_ClassifiesCorrectly()
        {
            // Arrange
            var message = "User is busy, all slots taken";

            // Act
            var error = TransferError.FromSoulseekRejection(message);

            // Assert
            Assert.Equal(TransferFailureReason.UserBusy, error.Reason);
            Assert.True(error.IsRetryable);
            Assert.Equal(TimeSpan.FromMinutes(5), error.SuggestedRetryDelay);
        }

        [Fact]
        public void GetUserFriendlyMessage_ReturnsAppropriateMessage()
        {
            // Arrange
            var error = new TransferError
            {
                Reason = TransferFailureReason.ConnectionTimeout,
                Message = "Technical timeout message"
            };

            // Act
            var message = error.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("Tiempo de conexión agotado", message);
            Assert.Contains("reintentará", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_DiskFull_ReturnsActionableMessage()
        {
            // Arrange
            var error = new TransferError
            {
                Reason = TransferFailureReason.DiskFull
            };

            // Act
            var message = error.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("Disco lleno", message);
            Assert.Contains("Libera espacio", message);
        }

        [Fact]
        public void GetUserFriendlyMessage_UserBanned_ReturnsClearMessage()
        {
            // Arrange
            var error = new TransferError
            {
                Reason = TransferFailureReason.UserBanned
            };

            // Act
            var message = error.GetUserFriendlyMessage();

            // Assert
            Assert.Contains("bloqueado", message);
        }

        [Fact]
        public void ToString_IncludesAllRelevantInfo()
        {
            // Arrange
            var error = new TransferError
            {
                Reason = TransferFailureReason.NetworkError,
                Message = "Network failure",
                IsRetryable = true,
                SuggestedRetryDelay = TimeSpan.FromMinutes(1)
            };

            // Act
            var str = error.ToString();

            // Assert
            Assert.Contains("NetworkError", str);
            Assert.Contains("Network failure", str);
            Assert.Contains("Retryable: True", str);
            Assert.Contains("00:01:00", str);
        }

        [Fact]
        public void FromException_NullException_ReturnsUnknown()
        {
            // Act
            var error = TransferError.FromException(null);

            // Assert
            Assert.Equal(TransferFailureReason.Unknown, error.Reason);
        }

        [Fact]
        public void FromSoulseekRejection_NullMessage_HandlesGracefully()
        {
            // Act
            var error = TransferError.FromSoulseekRejection(null);

            // Assert
            Assert.NotNull(error);
            Assert.Equal(TransferFailureReason.Unknown, error.Reason);
        }

        [Fact]
        public void RetryAttempts_TrackedCorrectly()
        {
            // Arrange
            var error = new TransferError
            {
                RetryAttempts = 0
            };

            // Act
            error.RetryAttempts++;
            error.RetryAttempts++;

            // Assert
            Assert.Equal(2, error.RetryAttempts);
        }

        [Fact]
        public void OccurredAt_SetToCurrentTime()
        {
            // Act
            var error = new TransferError();

            // Assert
            Assert.True((DateTime.UtcNow - error.OccurredAt).TotalSeconds < 1);
        }
    }
}
