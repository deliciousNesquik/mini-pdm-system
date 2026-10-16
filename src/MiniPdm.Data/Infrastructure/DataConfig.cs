using System.Runtime.CompilerServices;
using Dapper;

namespace MiniPdm.Data.Infrastructure;

internal static class DataConfig
{
    private static readonly Lazy<bool> _registered = new(() =>
    {
        SqlMapper.Settings.PreferTypeHandlersForEnums = true;
        SqlMapper.AddTypeHandler(new ObjectTypeHandler());
        SqlMapper.AddTypeHandler(new ObjectStateHandler());
        return true;
    }, LazyThreadSafetyMode.ExecutionAndPublication);

#pragma warning disable CA2255 // регистрация Dapper-хендлеров до любого кода сборки
    [ModuleInitializer]
    internal static void Init() => Register();
#pragma warning restore CA2255

    public static void Register() => _ = _registered.Value;
}