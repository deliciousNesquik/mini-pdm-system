namespace MiniPdm.Data.Infrastructure;

/// <summary>
/// Предоставляет исключение, которое возникает, когда база данных недоступна.
/// </summary>
public sealed class DatabaseUnavailableException : Exception
{
    public DatabaseUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}