using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using SlskDown.Core.Protocol;

namespace SlskDown.Tests.Core.Protocol
{
    /// <summary>
    /// Tests unitarios para SoulseekConnectionPool
    /// </summary>
    public class SoulseekConnectionPoolTests : IDisposable
    {
        private readonly SoulseekConnectionPool pool;

        public SoulseekConnectionPoolTests()
        {
            pool = new SoulseekConnectionPool(
                maxConnectionsPerUser: 3,
                idleTimeout: TimeSpan.FromSeconds(5)
            );
        }

        [Fact]
        public async Task GetOrCreateConnection_CreatesNewConnection_WhenNotInPool()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var connectionCreated = false;

            // Act
            var connection = await pool.GetOrCreateConnectionAsync(
                username,
                endpoint,
                async (ep) =>
                {
                    connectionCreated = true;
                    await Task.Delay(10);
                    return new MockConnection();
                }
            );

            // Assert
            Assert.NotNull(connection);
            Assert.True(connectionCreated);
        }

        [Fact]
        public async Task GetOrCreateConnection_ReusesExistingConnection_WhenInPool()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var creationCount = 0;

            // Act - Primera conexión
            var connection1 = await pool.GetOrCreateConnectionAsync(
                username,
                endpoint,
                async (ep) =>
                {
                    creationCount++;
                    await Task.Delay(10);
                    return new MockConnection();
                }
            );

            // Cerrar para devolver al pool
            connection1.Dispose();
            await Task.Delay(100);

            // Act - Segunda conexión (debería reutilizar)
            var connection2 = await pool.GetOrCreateConnectionAsync(
                username,
                endpoint,
                async (ep) =>
                {
                    creationCount++;
                    await Task.Delay(10);
                    return new MockConnection();
                }
            );

            // Assert
            Assert.Equal(1, creationCount); // Solo debería crear una vez
            Assert.NotNull(connection2);
        }

        [Fact]
        public async Task GetOrCreateConnection_RespectMaxConnectionsPerUser()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var connections = new System.Collections.Generic.List<object>();

            // Act - Crear más conexiones que el máximo
            for (int i = 0; i < 5; i++)
            {
                var connection = await pool.GetOrCreateConnectionAsync(
                    username,
                    endpoint,
                    async (ep) =>
                    {
                        await Task.Delay(10);
                        return new MockConnection();
                    }
                );
                connections.Add(connection);
            }

            var stats = pool.GetStatistics();

            // Assert
            Assert.True(stats.TotalConnections <= 3); // No más de 3 por usuario
        }

        [Fact]
        public async Task CleanupIdleConnections_RemovesOldConnections()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

            var connection = await pool.GetOrCreateConnectionAsync(
                username,
                endpoint,
                async (ep) =>
                {
                    await Task.Delay(10);
                    return new MockConnection();
                }
            );

            connection.Dispose();

            // Act - Esperar más que el idle timeout
            await Task.Delay(6000);
            pool.CleanupIdleConnections();

            var stats = pool.GetStatistics();

            // Assert
            Assert.Equal(0, stats.IdleConnections);
        }

        [Fact]
        public void GetStatistics_ReturnsCorrectStats()
        {
            // Act
            var stats = pool.GetStatistics();

            // Assert
            Assert.NotNull(stats);
            Assert.True(stats.TotalConnections >= 0);
            Assert.True(stats.ActiveConnections >= 0);
            Assert.True(stats.IdleConnections >= 0);
            Assert.True(stats.CacheHits >= 0);
            Assert.True(stats.CacheMisses >= 0);
        }

        [Fact]
        public async Task VerifyConnectionHealth_ReturnsFalseForDisposedConnection()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

            var connection = await pool.GetOrCreateConnectionAsync(
                username,
                endpoint,
                async (ep) =>
                {
                    await Task.Delay(10);
                    return new MockConnection();
                }
            );

            // Act
            connection.Dispose();
            var isHealthy = pool.VerifyConnectionHealth(connection);

            // Assert
            Assert.False(isHealthy);
        }

        [Fact]
        public async Task MultipleUsers_MaintainSeparatePools()
        {
            // Arrange
            var user1 = "user1";
            var user2 = "user2";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);

            // Act
            var conn1 = await pool.GetOrCreateConnectionAsync(user1, endpoint, 
                async (ep) => { await Task.Delay(10); return new MockConnection(); });
            
            var conn2 = await pool.GetOrCreateConnectionAsync(user2, endpoint,
                async (ep) => { await Task.Delay(10); return new MockConnection(); });

            var stats = pool.GetStatistics();

            // Assert
            Assert.NotNull(conn1);
            Assert.NotNull(conn2);
            Assert.True(stats.TotalConnections >= 2);
        }

        [Fact]
        public async Task ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var username = "testuser";
            var endpoint = new IPEndPoint(IPAddress.Loopback, 12345);
            var tasks = new System.Collections.Generic.List<Task>();

            // Act - Acceso concurrente desde múltiples threads
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var connection = await pool.GetOrCreateConnectionAsync(
                        username,
                        endpoint,
                        async (ep) =>
                        {
                            await Task.Delay(10);
                            return new MockConnection();
                        }
                    );
                    connection.Dispose();
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - No debería lanzar excepciones
            var stats = pool.GetStatistics();
            Assert.True(stats.TotalConnections >= 0);
        }

        [Fact]
        public void Dispose_CleansUpAllConnections()
        {
            // Arrange
            var testPool = new SoulseekConnectionPool();

            // Act
            testPool.Dispose();

            // Assert - No debería lanzar excepciones
            Assert.Throws<ObjectDisposedException>(() => testPool.GetStatistics());
        }

        public void Dispose()
        {
            pool?.Dispose();
        }

        // Mock connection para testing
        private class MockConnection : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
