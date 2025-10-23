using System.Reflection;
using Microsoft.Extensions.Logging;

namespace SxgEvalPlatformApi.Services.Cache
{
    /// <summary>
    /// Attribute to mark methods for caching
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CacheableAttribute : Attribute
    {
        public TimeSpan? ExpiryTime { get; set; }
        public string? KeyPattern { get; set; }
        public bool InvalidateOnWrite { get; set; } = true;
        
        public CacheableAttribute(int expiryMinutes = 30)
        {
            ExpiryTime = TimeSpan.FromMinutes(expiryMinutes);
        }
    }

    /// <summary>
    /// Attribute to mark methods that should invalidate cache
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class CacheInvalidateAttribute : Attribute
    {
        public string[] Patterns { get; set; }
        
        public CacheInvalidateAttribute(params string[] patterns)
        {
            Patterns = patterns;
        }
    }

    /// <summary>
    /// Generic caching decorator that can wrap any service using DispatchProxy
    /// </summary>
    /// <typeparam name="T">Service interface type</typeparam>
    public class CachingDecorator<T> : DispatchProxy where T : class
    {
        private T _target = null!;
        private IGenericCacheService _cacheService = null!;
        private ILogger _logger = null!;
        private string _servicePrefix = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod == null)
                return null;

            var cacheableAttr = targetMethod.GetCustomAttribute<CacheableAttribute>();
            var invalidateAttr = targetMethod.GetCustomAttribute<CacheInvalidateAttribute>();

            // Handle cache invalidation for write operations
            if (invalidateAttr != null)
            {
                return HandleInvalidatingMethod(targetMethod, args, invalidateAttr);
            }

            // Handle cacheable read operations
            if (cacheableAttr != null)
            {
                return HandleCacheableMethod(targetMethod, args, cacheableAttr);
            }

            // For non-cached methods, just call the target
            return targetMethod.Invoke(_target, args);
        }

        private object? HandleCacheableMethod(MethodInfo method, object?[]? args, CacheableAttribute cacheAttr)
        {
            try
            {
                var cacheKey = BuildCacheKey(method, args, cacheAttr.KeyPattern);
                var returnType = method.ReturnType;

                // Handle async methods
                if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    var resultType = returnType.GetGenericArguments()[0];
                    return HandleAsyncCacheableMethod(method, args, cacheKey, resultType, cacheAttr.ExpiryTime);
                }

                // Handle sync methods (though most will be async)
                return HandleSyncCacheableMethod(method, args, cacheKey, returnType, cacheAttr.ExpiryTime);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in caching decorator for method {Method}", method.Name);
                return method.Invoke(_target, args);
            }
        }

        private async Task<object?> HandleAsyncCacheableMethod(MethodInfo method, object?[]? args, string cacheKey, Type resultType, TimeSpan? expiry)
        {
            // Use reflection to call the generic GetOrSetAsync method
            var cacheMethod = _cacheService.GetType().GetMethod(nameof(IGenericCacheService.GetOrSetAsync))!
                .MakeGenericMethod(resultType);

            var factory = new Func<Task<object?>>(() => InvokeTargetMethodAsync(method, args));
            
            var result = await (Task<object?>)cacheMethod.Invoke(_cacheService, new object[] { cacheKey, factory, expiry })!;
            return result;
        }

        private object? HandleSyncCacheableMethod(MethodInfo method, object?[]? args, string cacheKey, Type returnType, TimeSpan? expiry)
        {
            // For sync methods, we'll just call through without caching for now
            // Most modern services should be async anyway
            return method.Invoke(_target, args);
        }

        private async Task<object?> InvokeTargetMethodAsync(MethodInfo method, object?[]? args)
        {
            var result = method.Invoke(_target, args);
            if (result is Task task)
            {
                await task;
                if (task.GetType().IsGenericType)
                {
                    return task.GetType().GetProperty("Result")!.GetValue(task);
                }
                return null;
            }
            return result;
        }

        private object? HandleInvalidatingMethod(MethodInfo method, object?[]? args, CacheInvalidateAttribute invalidateAttr)
        {
            try
            {
                // Execute the method first
                var result = method.Invoke(_target, args);

                // Then invalidate cache patterns
                Task.Run(async () =>
                {
                    foreach (var pattern in invalidateAttr.Patterns)
                    {
                        var fullPattern = $"{_servicePrefix}:{pattern}";
                        await _cacheService.InvalidatePatternAsync(fullPattern);
                        _logger.LogDebug("Invalidated cache pattern: {Pattern}", fullPattern);
                    }
                });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in invalidating method {Method}", method.Name);
                throw;
            }
        }

        private string BuildCacheKey(MethodInfo method, object?[]? args, string? keyPattern)
        {
            if (!string.IsNullOrEmpty(keyPattern))
            {
                // Use custom key pattern if provided
                return $"{_servicePrefix}:{keyPattern}";
            }

            // Generate key from method name and parameters
            var keyParts = new List<string> { method.Name };
            
            if (args != null)
            {
                keyParts.AddRange(args.Where(a => a != null).Select(a => a!.ToString()!));
            }

            return _cacheService.BuildCacheKey(_servicePrefix, keyParts.ToArray());
        }

        public static T Create(T target, IGenericCacheService cacheService, ILogger logger)
        {
            var proxy = Create<T, CachingDecorator<T>>() as CachingDecorator<T>;
            proxy!._target = target;
            proxy._cacheService = cacheService;
            proxy._logger = logger;
            proxy._servicePrefix = typeof(T).Name.Replace("Service", "").Replace("I", "").ToLower();
            
            return proxy as T;
        }
    }
}