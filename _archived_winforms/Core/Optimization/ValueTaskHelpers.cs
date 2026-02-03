using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Helpers para ValueTask - reduce allocations en operaciones async
    /// ValueTask es más eficiente que Task cuando el resultado ya está disponible
    /// </summary>
    public static class ValueTaskHelpers
    {
        /// <summary>
        /// Convierte resultado síncrono a ValueTask (sin allocation)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T> FromResult<T>(T result)
        {
            return new ValueTask<T>(result);
        }
        
        /// <summary>
        /// ValueTask completado (sin allocation)
        /// </summary>
        public static ValueTask CompletedTask => default;
        
        /// <summary>
        /// Convierte Task a ValueTask
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ValueTask<T> AsValueTask<T>(this Task<T> task)
        {
            return new ValueTask<T>(task);
        }
        
        /// <summary>
        /// Ejecuta con timeout usando ValueTask
        /// </summary>
        public static async ValueTask<T> WithTimeoutAsync<T>(
            this ValueTask<T> task,
            TimeSpan timeout)
        {
            var delayTask = Task.Delay(timeout);
            var completedTask = await Task.WhenAny(task.AsTask(), delayTask).ConfigureAwait(false);
            
            if (completedTask == delayTask)
                throw new TimeoutException();
            
            return await task.ConfigureAwait(false);
        }
    }
}
