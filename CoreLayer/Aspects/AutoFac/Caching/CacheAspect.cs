using Castle.DynamicProxy;
using CoreLayer.CrossCuttingConcerns.Caching;
using CoreLayer.Utilities.Interceptors;
using CoreLayer.Utilities.IoC;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CoreLayer.Aspects.AutoFac.Caching;

public class CacheAspect : MethodInterception
{
    private readonly int _duration;
    private readonly ICacheManager _cacheManager;

    public CacheAspect(int duration = 300)
    {
        _duration = duration;
        _cacheManager = ServiceTool.ServiceProvider.GetService<ICacheManager>();
    }

    public override void Intercept(IInvocation invocation)
    {
        // 1. Key Oluşturma (Senin mevcut kodun)
        string methodName = $"{invocation.Method.ReflectedType.FullName}.{invocation.Method.Name}";
        var arguments = invocation.Arguments.Select(arg => arg != null && arg.GetType().IsClass
            ? JsonConvert.SerializeObject(arg, new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore })
            : arg?.ToString() ?? "<Null>").ToList();
        var key = $"{methodName}({string.Join(",", arguments)})";

        // 2. Cache Kontrolü
        if (_cacheManager.IsAdd(key))
        {
            var cacheValue = _cacheManager.Get(key);
            if (cacheValue != null)
            {
                // Eğer metot Task (Async) dönüyorsa
                if (typeof(Task).IsAssignableFrom(invocation.Method.ReturnType))
                {
                    var resultType = invocation.Method.ReturnType.GenericTypeArguments[0];
                    // Task.FromResult<T>(cacheValue) metodunu çağırıyoruz
                    invocation.ReturnValue = typeof(Task).GetMethod("FromResult")
                        ?.MakeGenericMethod(resultType)
                        .Invoke(null, new[] { cacheValue });
                }
                else
                {
                    invocation.ReturnValue = cacheValue;
                }
                return;
            }
        }

        // 3. Metodu Çalıştır
        invocation.Proceed();

        // 4. ASENKRON DÖNÜŞ YÖNETİMİ (Burada reflection ile hataları çözüyoruz)
        if (invocation.ReturnValue is Task task)
        {
            var resultType = invocation.Method.ReturnType.GenericTypeArguments[0];

            var method = typeof(CacheAspect).GetMethod(nameof(HandleAsyncCache),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var genericMethod = method.MakeGenericMethod(resultType);

            var ct = invocation.Arguments.OfType<CancellationToken>().FirstOrDefault();

            invocation.ReturnValue = genericMethod.Invoke(this, [task, key, ct]);
        }
        else
        {
            // Senkron işlemler için direkt cache'le
            if (invocation.ReturnValue != null)
                _cacheManager.Add(key, invocation.ReturnValue, _duration);
        }
    }

    private async Task<T> HandleAsyncCache<T>(Task<T> task, string key, CancellationToken ct = default)
    {
        try
        {
            var result = await task.WaitAsync(ct);

            if (result != null)
            {
                _cacheManager.Add(key, result, _duration);
            }
            return result;
        }
        catch
        {
            throw;
        }
    }
}