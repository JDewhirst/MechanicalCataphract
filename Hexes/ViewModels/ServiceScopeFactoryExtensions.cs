using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace GUI.ViewModels;

internal static class ServiceScopeFactoryExtensions
{
    public static async Task InScopeAsync(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, Task> operation)
    {
        using var scope = scopeFactory.CreateScope();
        await operation(scope.ServiceProvider);
    }

    public static async Task<T> InScopeAsync<T>(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, Task<T>> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return await operation(scope.ServiceProvider);
    }

    public static T InScope<T>(
        this IServiceScopeFactory scopeFactory,
        Func<IServiceProvider, T> operation)
    {
        using var scope = scopeFactory.CreateScope();
        return operation(scope.ServiceProvider);
    }
}
