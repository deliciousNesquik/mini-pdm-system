using MiniPdm.Core.Domain;

namespace MiniPdm.Tests.Domain;

public sealed class DesignationRulesTests
{
    [Theory]
    [InlineData("АБВГ.301245.001")]
    [InlineData("РДЦЛ.304112.300")]      // пример из ТЗ
    [InlineData("АБВЁ.301245.001")]      // Ё — до «А» в Unicode, включена явно
    [InlineData("ЯЯЯЯ.999999.999")]
    public void Valid_designations_pass(string designation) =>
        Assert.True(DesignationRules.IsValid(designation));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("абвг.301245.001")]      // строчные
    [InlineData("ABCD.301245.001")]      // латиница целиком
    [InlineData("PДЦЛ.304112.601")]      // латинская P
    [InlineData("РДЦЛ.30411.602")]       // пять цифр
    [InlineData("АБВГ.301245.0012")]     // семь цифр
    [InlineData("АБВГ.301245.01")]       // две цифры в конце
    [InlineData("АБВГ-301245.001")]      // не тот разделитель
    [InlineData("АБВГ.301245.001 ")]     // хвостовой пробел
    [InlineData(" АБВГ.301245.001")]     // ведущий пробел
    public void Invalid_designations_fail(string? designation) =>
        Assert.False(DesignationRules.IsValid(designation));
}