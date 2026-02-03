using System;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace SlskDown.Core.Optimization
{
    /// <summary>
    /// Caché de reflexión para evitar overhead de reflection repetida
    /// Hasta 100x más rápido que reflection directa
    /// </summary>
    public static class ReflectionCache
    {
        private static readonly ConcurrentDictionary<string, Delegate> propertyGetters = new();
        private static readonly ConcurrentDictionary<string, Delegate> propertySetters = new();
        private static readonly ConcurrentDictionary<string, Delegate> methodInvokers = new();
        
        /// <summary>
        /// Obtiene getter compilado para propiedad
        /// </summary>
        public static Func<T, TProperty> GetPropertyGetter<T, TProperty>(string propertyName)
        {
            var key = $"{typeof(T).FullName}.{propertyName}";
            
            return (Func<T, TProperty>)propertyGetters.GetOrAdd(key, _ =>
            {
                var parameter = Expression.Parameter(typeof(T), "obj");
                var property = Expression.Property(parameter, propertyName);
                var lambda = Expression.Lambda<Func<T, TProperty>>(property, parameter);
                return lambda.Compile();
            });
        }
        
        /// <summary>
        /// Obtiene setter compilado para propiedad
        /// </summary>
        public static Action<T, TProperty> GetPropertySetter<T, TProperty>(string propertyName)
        {
            var key = $"{typeof(T).FullName}.{propertyName}";
            
            return (Action<T, TProperty>)propertySetters.GetOrAdd(key, _ =>
            {
                var parameter = Expression.Parameter(typeof(T), "obj");
                var valueParameter = Expression.Parameter(typeof(TProperty), "value");
                var property = Expression.Property(parameter, propertyName);
                var assign = Expression.Assign(property, valueParameter);
                var lambda = Expression.Lambda<Action<T, TProperty>>(assign, parameter, valueParameter);
                return lambda.Compile();
            });
        }
        
        /// <summary>
        /// Obtiene invoker compilado para método
        /// </summary>
        public static Func<T, TResult> GetMethodInvoker<T, TResult>(string methodName)
        {
            var key = $"{typeof(T).FullName}.{methodName}";
            
            return (Func<T, TResult>)methodInvokers.GetOrAdd(key, _ =>
            {
                var parameter = Expression.Parameter(typeof(T), "obj");
                var method = typeof(T).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
                var call = Expression.Call(parameter, method);
                var lambda = Expression.Lambda<Func<T, TResult>>(call, parameter);
                return lambda.Compile();
            });
        }
        
        /// <summary>
        /// Crea instancia rápida de tipo
        /// </summary>
        public static Func<T> GetConstructor<T>() where T : new()
        {
            var key = $"ctor_{typeof(T).FullName}";
            
            return (Func<T>)methodInvokers.GetOrAdd(key, _ =>
            {
                var newExpression = Expression.New(typeof(T));
                var lambda = Expression.Lambda<Func<T>>(newExpression);
                return lambda.Compile();
            });
        }
        
        /// <summary>
        /// Limpia caché
        /// </summary>
        public static void Clear()
        {
            propertyGetters.Clear();
            propertySetters.Clear();
            methodInvokers.Clear();
        }
        
        /// <summary>
        /// Obtiene estadísticas de caché
        /// </summary>
        public static (int Getters, int Setters, int Methods) GetStats()
        {
            return (propertyGetters.Count, propertySetters.Count, methodInvokers.Count);
        }
    }
}
