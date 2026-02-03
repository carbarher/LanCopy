using System;
using System.Threading.Tasks;
using Xunit;
using SlskDown.Core.Events;

namespace SlskDown.Tests.Core.Events
{
    public class NetworkEventBusTests : IDisposable
    {
        private readonly NetworkEventBus eventBus;

        public NetworkEventBusTests()
        {
            eventBus = new NetworkEventBus();
        }

        [Fact]
        public void Subscribe_AddsHandler_Successfully()
        {
            // Arrange
            var handlerCalled = false;
            Action<TestMessage> handler = msg => handlerCalled = true;

            // Act
            eventBus.Subscribe(handler);
            eventBus.Publish(new TestMessage());

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public void Publish_CallsAllSubscribers()
        {
            // Arrange
            var count = 0;
            eventBus.Subscribe<TestMessage>(msg => count++);
            eventBus.Subscribe<TestMessage>(msg => count++);
            eventBus.Subscribe<TestMessage>(msg => count++);

            // Act
            eventBus.Publish(new TestMessage());

            // Assert
            Assert.Equal(3, count);
        }

        [Fact]
        public void Unsubscribe_RemovesHandler()
        {
            // Arrange
            var handlerCalled = false;
            Action<TestMessage> handler = msg => handlerCalled = true;

            eventBus.Subscribe(handler);
            eventBus.Unsubscribe(handler);

            // Act
            eventBus.Publish(new TestMessage());

            // Assert
            Assert.False(handlerCalled);
        }

        [Fact]
        public async Task SubscribeAsync_HandlesAsyncHandlers()
        {
            // Arrange
            var handlerCalled = false;
            Func<TestMessage, Task> handler = async msg =>
            {
                await Task.Delay(10);
                handlerCalled = true;
            };

            // Act
            eventBus.SubscribeAsync(handler);
            await eventBus.PublishAsync(new TestMessage());

            // Assert
            Assert.True(handlerCalled);
        }

        [Fact]
        public async Task PublishAsync_WaitsForAllHandlers()
        {
            // Arrange
            var count = 0;
            eventBus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(50);
                count++;
            });
            eventBus.SubscribeAsync<TestMessage>(async msg =>
            {
                await Task.Delay(50);
                count++;
            });

            // Act
            await eventBus.PublishAsync(new TestMessage());

            // Assert
            Assert.Equal(2, count);
        }

        [Fact]
        public void Publish_HandlesExceptionsGracefully()
        {
            // Arrange
            var goodHandlerCalled = false;
            eventBus.Subscribe<TestMessage>(msg => throw new Exception("Test error"));
            eventBus.Subscribe<TestMessage>(msg => goodHandlerCalled = true);

            // Act
            eventBus.Publish(new TestMessage());

            // Assert - El segundo handler debería ejecutarse a pesar del error
            Assert.True(goodHandlerCalled);
        }

        [Fact]
        public void HandlerError_EventFired_OnException()
        {
            // Arrange
            Exception caughtException = null;
            eventBus.HandlerError += (sender, args) => caughtException = args.Exception;
            eventBus.Subscribe<TestMessage>(msg => throw new InvalidOperationException("Test"));

            // Act
            eventBus.Publish(new TestMessage());

            // Assert
            Assert.NotNull(caughtException);
            Assert.IsType<InvalidOperationException>(caughtException);
        }

        [Fact]
        public void GetSubscriberCount_ReturnsCorrectCount()
        {
            // Arrange
            eventBus.Subscribe<TestMessage>(msg => { });
            eventBus.Subscribe<TestMessage>(msg => { });
            eventBus.SubscribeAsync<TestMessage>(async msg => await Task.CompletedTask);

            // Act
            var count = eventBus.GetSubscriberCount<TestMessage>();

            // Assert
            Assert.Equal(3, count);
        }

        [Fact]
        public void Clear_RemovesAllSubscriptions()
        {
            // Arrange
            eventBus.Subscribe<TestMessage>(msg => { });
            eventBus.Subscribe<TestMessage>(msg => { });

            // Act
            eventBus.Clear();
            var count = eventBus.GetSubscriberCount<TestMessage>();

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void MultipleMessageTypes_WorkIndependently()
        {
            // Arrange
            var message1Called = false;
            var message2Called = false;

            eventBus.Subscribe<TestMessage>(msg => message1Called = true);
            eventBus.Subscribe<AnotherTestMessage>(msg => message2Called = true);

            // Act
            eventBus.Publish(new TestMessage());

            // Assert
            Assert.True(message1Called);
            Assert.False(message2Called);
        }

        [Fact]
        public async Task ConcurrentPublish_ThreadSafe()
        {
            // Arrange
            var count = 0;
            var lockObj = new object();
            eventBus.Subscribe<TestMessage>(msg =>
            {
                lock (lockObj) { count++; }
            });

            // Act
            var tasks = new Task[100];
            for (int i = 0; i < 100; i++)
            {
                tasks[i] = Task.Run(() => eventBus.Publish(new TestMessage()));
            }
            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(100, count);
        }

        [Fact]
        public void Publish_WithNullMessage_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => eventBus.Publish<TestMessage>(null));
        }

        [Fact]
        public void Subscribe_WithNullHandler_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => eventBus.Subscribe<TestMessage>(null));
        }

        public void Dispose()
        {
            eventBus?.Dispose();
        }

        private class TestMessage { }
        private class AnotherTestMessage { }
    }
}
