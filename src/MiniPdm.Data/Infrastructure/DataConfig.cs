using Dapper;

namespace MiniPdm.Data.Infrastructure;

internal static class DataConfig
{
    private static readonly Lazy<bool> _registered = new(
        () => { SqlMapper.AddTypeHandler(new ObjectTypeHandler());
            SqlMapper.AddTypeHandler(new ObjectStateHandler());
            return true; },
        LazyThreadSafetyMode.ExecutionAndPublication);
    
    public static void Register() => _ = _registered.Value;
}