using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.Shared;
using Xunit;

namespace GodotSharpDI.SourceGenerator.Tests.Shared;

/// <summary>
/// NamingHelper comprehensive unit tests
///
/// Coverage:
///   - ToPascalCase: member name → PascalCase
///   - GetFailureCallbackMethodName: → OnXxxInjectionFailed
///   - GetReadyCallbackMethodName: → OnXxxInjectionReady
///   - GetInjectionCallbackListName (new in refactoring): → __xxx_callbacks, used for WaitFor callback list fields
///   - GetInjectionReadyFieldName: → IsXxxInjectionReady
/// </summary>
public class NamingHelperTests
{
    // ============================================================
    //  ToPascalCase
    // ============================================================

    [Theory]
    [InlineData("_myField", "MyField")]
    [InlineData("_my_service", "MyService")]
    [InlineData("MyProperty", "MyProperty")]
    [InlineData("myField", "MyField")]
    [InlineData("__doubleUnder", "DoubleUnder")]
    [InlineData("_a", "A")]
    [InlineData("abc", "Abc")]
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
    [InlineData("_service", "OnServiceInjectionFailed")]
    [InlineData("_playerStats", "OnPlayerStatsInjectionFailed")]
    [InlineData("_my_component", "OnMyComponentInjectionFailed")]
    [InlineData("MyProperty", "OnMyPropertyInjectionFailed")]
    public void GetFailureCallbackMethodName_VariousInputs_ReturnsExpected(
        string input,
        string expected
    )
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
    [InlineData("_service", "OnServiceInjectionReady")]
    [InlineData("_playerStats", "OnPlayerStatsInjectionReady")]
    [InlineData("_my_component", "OnMyComponentInjectionReady")]
    [InlineData("MyProperty", "OnMyPropertyInjectionReady")]
    public void GetReadyCallbackMethodName_VariousInputs_ReturnsExpected(
        string input,
        string expected
    )
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
    //  GetInjectionCallbackListName (refactoring: replaces original GetInjectionTcsName)
    // ============================================================

    [Theory]
    [InlineData("_config", "__config_callbacks")]
    [InlineData("_playerStatsService", "__playerStatsService_callbacks")]
    [InlineData("_my_service", "__myService_callbacks")]
    [InlineData("MyProp", "__myProp_callbacks")]
    [InlineData("_a", "__a_callbacks")]
    public void GetInjectionCallbackListName_VariousInputs_ReturnsExpected(
        string input,
        string expected
    )
    {
        Assert.Equal(expected, NamingHelper.GetInjectionCallbackListName(input));
    }

    [Fact]
    public void GetInjectionCallbackListName_UseDoubleUnderscorePrefix_AvoidsMemberNameCollision()
    {
        // Generated name must start with __ to avoid collision with user fields (_xxx) or local variables
        var result = NamingHelper.GetInjectionCallbackListName("_service");
        Assert.StartsWith("__", result);
        Assert.EndsWith("_callbacks", result);
    }

    [Fact]
    public void GetInjectionCallbackListName_EmptyMember_ReturnsFallback()
    {
        var result = NamingHelper.GetInjectionCallbackListName("_");
        // After stripping all leading underscores, it's empty → use fallback value
        Assert.Equal("__callbacks", result);
    }

    [Fact]
    public void GetInjectionCallbackListName_DifferentMembers_ProduceDifferentNames()
    {
        // Ensure two different fields produce different callback list names (no collision)
        var a = NamingHelper.GetInjectionCallbackListName("_serviceA");
        var b = NamingHelper.GetInjectionCallbackListName("_serviceB");
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GetInjectionCallbackListName_SameMember_IsDeterministic()
    {
        // Same field called multiple times should return the same value
        var first = NamingHelper.GetInjectionCallbackListName("_config");
        var second = NamingHelper.GetInjectionCallbackListName("_config");
        Assert.Equal(first, second);
    }

    // ============================================================
    //  GetInjectionReadyFieldName
    // ============================================================

    [Theory]
    [InlineData("_service", "IsServiceInjectionReady")]
    [InlineData("_playerStats", "IsPlayerStatsInjectionReady")]
    [InlineData("_my_component", "IsMyComponentInjectionReady")]
    [InlineData("MyProperty", "IsMyPropertyInjectionReady")]
    public void GetInjectionReadyFieldName_VariousInputs_ReturnsExpected(
        string input,
        string expected
    )
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
    //  Consistency constraint: different methods produce distinct non-conflicting names for the same input
    // ============================================================

    [Fact]
    public void AllMethods_SameInput_ProduceDistinctNames()
    {
        const string member = "_service";

        var pascal = NamingHelper.ToPascalCase(member);
        var callbackList = NamingHelper.GetInjectionCallbackListName(member);
        var readyFn = NamingHelper.GetReadyCallbackMethodName(member);
        var failFn = NamingHelper.GetFailureCallbackMethodName(member);
        var readyFlag = NamingHelper.GetInjectionReadyFieldName(member);

        // All generated names are distinct
        var names = new[] { pascal, callbackList, readyFn, failFn, readyFlag };
        Assert.Equal(names.Length, names.Distinct().Count());
    }
}
