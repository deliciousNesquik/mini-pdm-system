using System.Data;
using Dapper;
using MiniPdm.Core.Domain;

namespace MiniPdm.Data.Infrastructure;


/// <summary>
/// Обработчик типа ObjectType для Dapper, обеспечивающий корректное преобразование
/// между значениями перечисления ObjectType и их строковыми представлениями в базе данных.
/// </summary>
internal sealed class ObjectTypeHandler : SqlMapper.TypeHandler<ObjectType>
{
    public override void SetValue(IDbDataParameter parameter, ObjectType value) =>
        parameter.Value = value.ToString();

    public override ObjectType Parse(object value) =>
        Enum.TryParse<ObjectType>((string)value, out var parsed) && Enum.IsDefined(parsed)
            ? parsed
            : throw new InvalidOperationException($"Неизвестное значение object_type в БД: «{value}»");
}