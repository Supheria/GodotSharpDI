using System.Linq;
using GodotSharpDI.SourceGenerator.Shared;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Shared;

/// <summary>
/// NamingHelper 完整单元测试
///
/// 覆盖：
///   - ToPascalCase：成员名 → 大驼峰
///   - GetFailureCallbackMethodName：→ OnXxxInjectionFailed
///   - GetReadyCallbackMethodName：→ OnXxxInjectionReady
///   - GetInjectionTcsName（P3 新增）：→ __xxx_tcs，用于 WaitFor TCS 局部变量
///   - GetInjectionReadyFieldName：→ IsXxxInjectionReady
/// </summary>
public class NamingHelperTests
{
    // ============================================================
    //  ToPascalCase
    // ============================================================

    [Theory]
    [InlineData("_myField",      "MyField")]
    [InlineData("_my_service",   "MyService")]
    [InlineData("MyProperty",    "MyProperty")]
    [InlineData("myField",       "MyField")]
    [InlineData("__doubleUnder", "DoubleUnder")]
    [InlineData("_a",            "A")]
    [InlineData("abc",           "Abc")]
    public void ToPascalCase_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.ToPascalCase(input));
    }

    [Fact]
    public void ToPascalCase_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NamingHelper.ToPascalCase(""));
        Assert.Equal(string.Empty, NamingHelper.ToPascalCase(null!));
    }

    [Fact]
    public void ToPascalCase_OnlyUnderscores_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, NamingHelper.ToPascalCase("_"));
        Assert.Equal(string.Empty, NamingHelper.ToPascalCase("___"));
    }

    // ============================================================
    //  GetFailureCallbackMethodName
    // ============================================================

    [Theory]
    [InlineData("_service",        "OnServiceInjectionFailed")]
    [InlineData("_playerStats",    "OnPlayerStatsInjectionFailed")]
    [InlineData("_my_component",   "OnMyComponentInjectionFailed")]
    [InlineData("MyProperty",      "OnMyPropertyInjectionFailed")]
    public void GetFailureCallbackMethodName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetFailureCallbackMethodName(input));
    }

    [Fact]
    public void GetFailureCallbackMethodName_EmptyMember_ReturnsDefaultName()
    {
        Assert.Equal("OnInjectionFailed", NamingHelper.GetFailureCallbackMethodName(""));
        Assert.Equal("OnInjectionFailed", NamingHelper.GetFailureCallbackMethodName("_"));
    }

    // ============================================================
    //  GetReadyCallbackMethodName
    // ============================================================

    [Theory]
    [InlineData("_service",        "OnServiceInjectionReady")]
    [InlineData("_playerStats",    "OnPlayerStatsInjectionReady")]
    [InlineData("_my_component",   "OnMyComponentInjectionReady")]
    [InlineData("MyProperty",      "OnMyPropertyInjectionReady")]
    public void GetReadyCallbackMethodName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetReadyCallbackMethodName(input));
    }

    [Fact]
    public void GetReadyCallbackMethodName_EmptyMember_ReturnsDefaultName()
    {
        Assert.Equal("OnInjectionReady", NamingHelper.GetReadyCallbackMethodName(""));
        Assert.Equal("OnInjectionReady", NamingHelper.GetReadyCallbackMethodName("_"));
    }

    // ============================================================
    //  GetInjectionTcsName（P3 新增）
    // ============================================================

    [Theory]
    [InlineData("_config",              "__config_tcs")]
    [InlineData("_playerStatsService",  "__playerStatsService_tcs")]
    [InlineData("_my_service",          "__myService_tcs")]
    [InlineData("MyProp",               "__myProp_tcs")]
    [InlineData("_a",                   "__a_tcs")]
    public void GetInjectionTcsName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetInjectionTcsName(input));
    }

    [Fact]
    public void GetInjectionTcsName_UseDoubleUnderscorePrefix_AvoidsMemberNameCollision()
    {
        // 生成名必须以 __ 开头，避免与用户字段（_xxx）或局部变量冲突
        var result = NamingHelper.GetInjectionTcsName("_service");
        Assert.StartsWith("__", result);
        Assert.EndsWith("_tcs", result);
    }

    [Fact]
    public void GetInjectionTcsName_EmptyMember_ReturnsFallback()
    {
        var result = NamingHelper.GetInjectionTcsName("_");
        // 前导下划线全剥离后为空 → 使用兜底值
        Assert.Equal("__tcs", result);
    }

    [Fact]
    public void GetInjectionTcsName_DifferentMembers_ProduceDifferentNames()
    {
        // 确保两个不同字段的 TCS 名不同（无碰撞）
        var a = NamingHelper.GetInjectionTcsName("_serviceA");
        var b = NamingHelper.GetInjectionTcsName("_serviceB");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetInjectionTcsName_SameMember_IsDeterministic()
    {
        // 同一字段多次调用应返回相同值
        var first  = NamingHelper.GetInjectionTcsName("_config");
        var second = NamingHelper.GetInjectionTcsName("_config");
        Assert.Equal(first, second);
    }

    // ============================================================
    //  GetInjectionReadyFieldName
    // ============================================================

    [Theory]
    [InlineData("_service",        "IsServiceInjectionReady")]
    [InlineData("_playerStats",    "IsPlayerStatsInjectionReady")]
    [InlineData("_my_component",   "IsMyComponentInjectionReady")]
    [InlineData("MyProperty",      "IsMyPropertyInjectionReady")]
    public void GetInjectionReadyFieldName_VariousInputs_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, NamingHelper.GetInjectionReadyFieldName(input));
    }

    [Fact]
    public void GetInjectionReadyFieldName_EmptyMember_ReturnsDefaultName()
    {
        Assert.Equal("IsInjectionReady", NamingHelper.GetInjectionReadyFieldName(""));
        Assert.Equal("IsInjectionReady", NamingHelper.GetInjectionReadyFieldName("_"));
    }

    // ============================================================
    //  一致性约束：不同方法对同一输入产生独立不冲突的名称
    // ============================================================

    [Fact]
    public void AllMethods_SameInput_ProduceDistinctNames()
    {
        const string member = "_service";

        var pascal    = NamingHelper.ToPascalCase(member);
        var tcsName   = NamingHelper.GetInjectionTcsName(member);
        var readyFn   = NamingHelper.GetReadyCallbackMethodName(member);
        var failFn    = NamingHelper.GetFailureCallbackMethodName(member);
        var readyFlag = NamingHelper.GetInjectionReadyFieldName(member);

        // 所有生成名均不相同
        var names = new[] { pascal, tcsName, readyFn, failFn, readyFlag };
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
