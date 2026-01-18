using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.DiBuild;

internal static class DiagnosticDescriptors
{
    private const string Category = "GodotSharp.DI";

    // ============================================================
    // GDI_C001 ~ 099: 类型级错误
    // ============================================================

    public static readonly DiagnosticDescriptor ServiceLifetimeConflict = new(
        id: "GDI_C001",
        title: "服务生命周期标记冲突",
        messageFormat: "纯服务类型 {0} 不能同时标记为 [Singleton] 和 [Transient]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ServiceCannotBeNode = new(
        id: "GDI_C002",
        title: "服务不能是 Godot 节点类型",
        messageFormat: "{0} 类型 {1} 不能是 Godot 节点类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeMustBeNode = new(
        id: "GDI_C003",
        title: "Scope 必须是 Godot 节点类型",
        messageFormat: "Scope 类型 {0} 必须是 Godot 节点类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidAttribute = new(
        id: "GDI_C004",
        title: "无效的标记",
        messageFormat: "{0} 类型 {1} 不能标记为 {2}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ServiceReadyNeedUser = new(
        id: "GDI_C005",
        title: "IServiceReady 未标记为 [User]",
        messageFormat: "类型 {0} 实现了 IServiceReady 接口，但没有标记为 [User]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    // ============================================================
    // GDI_S200 ~ 299: 服务语义错误
    // ============================================================

    public static readonly DiagnosticDescriptor NoPublicConstructor = new(
        id: "GDI_S200",
        title: "服务必须有非静态构造函数",
        messageFormat: "服务类型 {0} 必须至少有一个非静态构造函数（不限制 public 或 private）",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidInjectConstructorAttribute = new(
        id: "GDI_S201",
        title: "无效的注入构造函数标记",
        messageFormat: "类型 {0} 必须标记为 [Singleton] 或 [Transient] 才能将构造函数标记为 [InjectConstructor]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor AmbiguousConstructor = new(
        id: "GDI_S202",
        title: "不明确的注入构造函数",
        messageFormat: "服务类型 {0} 必须且只能标记唯一的 [InjectConstructor]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // ============================================================
    // GDI_M300 ~ 399: 成员注入错误
    // ============================================================

    public static readonly DiagnosticDescriptor InvalidMemberAttribute = new(
        id: "GDI_M300",
        title: "无效的成员标记",
        messageFormat: "类型 {0} 必须标记为 [{1}] 才能将成员标记为 [{2}]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MemberAttributeConflict = new(
        id: "GDI_M301",
        title: "标记冲突",
        messageFormat: "不能同时标记为 [{0}] 和 [{1}]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor SingletonPropertyNotAccessible = new(
        id: "GDI_M302",
        title: "标记为 [Singleton] 的属性需要有 getter",
        messageFormat: "标记为 [Singleton] 的属性需要有 getter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InjectMemberNotAssignable = new(
        id: "GDI_M303",
        title: "标记为 [Inject] 的成员不能是只读的",
        messageFormat: "标记为 [Inject] 的成员不能是只读的",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // ============================================================
    // GDI_P400 ~ 499: Scope 语义错误
    // ============================================================

    public static readonly DiagnosticDescriptor InvalidModuleAttribute = new(
        id: "GDI_P400",
        title: "无效的 Models 标记",
        messageFormat: "类型 {0} 只有实现了 IScope 接口才能标记为 [Modules] 或 [AutoModules]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeModulesConflict = new(
        id: "GDI_P401",
        title: "Models 标记冲突",
        messageFormat: "Scope 类型 {0} 不能同时标记为 [Modules] 和 [AutoModules]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeMissingModules = new(
        id: "GDI_P402",
        title: "Scope 缺少 Models 标记",
        messageFormat: "Scope 类型 {0} 必须标记为 [Modules] 或 [AutoModules]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeModulesInstantiateEmpty = new(
        id: "GDI_P403",
        title: "Scope 缺少服务实例",
        messageFormat: "Scope 类型 {0} 没有在 [Modules] Instantiate 中指定任何服务类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeModulesExpectEmpty = new(
        id: "GDI_P404",
        title: "Scope 缺少 Host 实例",
        messageFormat: "Scope 类型 {0} 没有在 [Modules] Expect 中指定任何 Host 类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    // ============================================================
    // GDI_D500 ~ 599: 依赖图错误
    // ============================================================

    public static readonly DiagnosticDescriptor ScopeServiceNotDefined = new(
        id: "GDI_D500",
        title: "未定义的 Scope 服务类型",
        messageFormat: "在 Scope 类型 {0} 的 [Modules] Instantiate 中指定的类型 {1} 不是服务类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ScopeExpectNotHost = new(
        id: "GDI_D501",
        title: "Scope Expect 非 Host 类型",
        messageFormat: "在 Scope 类型 {0} 的 [Modules] Expect 中指定的类型 {1} 不是 Host 类型",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateServiceRegistration = new(
        "DI102",
        "Duplicate service registration",
        "Service type '{0}' is registered by multiple implementations: {1}",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor SingletonDependsOnTransient = new(
        "DI103",
        "Singleton service depends on transient service",
        "Singleton service '{0}' depends on transient service '{1}' ({2})",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ServiceNotRegistered = new(
        "DI104",
        "Required service not registered",
        "Service type '{0}' required by '{1}' is not registered",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor CircularDependency = new(
        "DI105",
        "Circular dependency detected",
        "Circular dependency detected: {0}",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ScopeDependencyNotResolvable = new(
        "DI108",
        "Scope dependency not resolvable",
        "Service '{0}' required by '{1}' in scope '{2}' cannot be resolved",
        "DependencyInjection",
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor HostProvidesNoServices = new(
        "DI109",
        "Host provides no services",
        "Host '{0}' expected by scope '{1}' does not provide any services",
        "DependencyInjection",
        DiagnosticSeverity.Warning,
        true
    );

    public static readonly DiagnosticDescriptor UnusedService = new(
        "DI200",
        "Service is never used",
        "Service '{0}' is registered but never injected or used",
        "DependencyInjection",
        DiagnosticSeverity.Info,
        true
    );

    // ============================================================
    // GDI_G900 ~ 999: 生成器内部错误
    // ============================================================

    public static readonly DiagnosticDescriptor UnknownTypeRole = new(
        id: "GDI_G900",
        title: "未知的 Type Role",
        messageFormat: "类型 {0} 是未知的 Type Role",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor RequestCancellation = new(
        id: "GDI_G901",
        title: "请求取消构建",
        messageFormat: "源生成器在执行过程中收到取消请求：{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor GeneratorInternalError = new(
        id: "GDI_G902",
        title: "源生成器生成异常",
        messageFormat: "源生成器生成代码时遇到了未知异常：{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnexpectScopeWithoutModules = new(
        id: "GDI_G903",
        title: "意外的 Scope 类型",
        messageFormat: "意外的 Scope 类型 {0}：没有标记为 [Modules] 或 [AutoModules]",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor MultipleInjectConstructors = new(
        "DI010",
        "Multiple constructors marked with InjectConstructor",
        "Service type '{0}' has multiple constructors marked with [InjectConstructor]",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    // 构造函数相关
    public static readonly DiagnosticDescriptor ConstructorCandidate = new(
        "DI012",
        "Constructor candidate",
        "Constructor '{0}' is a candidate for injection. Consider marking it with [InjectConstructor]",
        Category,
        DiagnosticSeverity.Info,
        true
    );

    public static readonly DiagnosticDescriptor ValueTypeParameter = new(
        "DI013",
        "Constructor parameter cannot be value type",
        "Parameter '{0}' of type '{1}' in '{2}' cannot be a value type",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ConcreteTypeDependency = new(
        "DI014",
        "Dependency on concrete type",
        "Parameter '{0}' depends on concrete type '{1}'. Consider depending on an interface or abstract class",
        Category,
        DiagnosticSeverity.Warning,
        true
    );

    // 成员相关

    public static readonly DiagnosticDescriptor MemberCannotBeValueType = new(
        "DI017",
        "Member cannot be value type",
        "Member '{0}' of type '{1}' in '{2}' cannot be a value type",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor MemberShouldBeInterface = new(
        "DI018",
        "Member should be interface type",
        "Member '{0}' depends on concrete type '{1}'. Consider using an interface",
        Category,
        DiagnosticSeverity.Warning,
        true
    );

    // Scope 相关

    public static readonly DiagnosticDescriptor ScopeMustImplementInterface = new(
        "DI019",
        "Scope must implement IScope",
        "Type '{0}' marked as Scope must implement '{1}' interface",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ModulesMissingInstantiate = new(
        "DI020",
        "Modules attribute missing Instantiate parameter",
        "Scope '{0}' must specify Instantiate parameter in [Modules] attribute",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ModulesInstantiateEmpty = new(
        "DI021",
        "Modules Instantiate is empty",
        "Scope '{0}' has empty Instantiate array in [Modules] attribute",
        Category,
        DiagnosticSeverity.Warning,
        true
    );

    public static readonly DiagnosticDescriptor ModulesInstantiateNotService = new(
        "DI022",
        "Instantiate type must be a service",
        "Type '{0}' in Instantiate of scope '{1}' must be marked with [Singleton] or [Transient]",
        Category,
        DiagnosticSeverity.Error,
        true
    );

    public static readonly DiagnosticDescriptor ModulesExpectNotHost = new(
        "DI023",
        "Expect type must be a host",
        "Type '{0}' in Expect of scope '{1}' must be marked with [Host]",
        Category,
        DiagnosticSeverity.Error,
        true
    );
}
