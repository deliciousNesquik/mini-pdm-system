using System.Data;
using Dapper;
using MiniPdm.Core.Domain;

namespace MiniPdm.Data;

/// <summary>
/// Обработчик типа ObjectState для Dapper, обеспечивающий корректное преобразование
/// между значениями перечисления ObjectState и их строковыми представлениями в базе данных.
/// </summary>
internal sealed class ObjectStateHandler : SqlMapper.TypeHandler<ObjectState>
{
    public override void SetValue(IDbDataParameter parameter, ObjectState value) =>
        parameter.Value = value.ToString();

    public override ObjectState Parse(object value) =>
        Enum.TryParse<ObjectState>((string)value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Неизвестное значение state в БД: «{value}»");
}