using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: AssemblyApi <assembly-path>");
    return 2;
}

string assemblyPath = Path.GetFullPath(args[0]);
if (!File.Exists(assemblyPath))
{
    Console.Error.WriteLine($"Assembly not found: {assemblyPath}");
    return 2;
}

var loadContext = new InspectionLoadContext(Path.GetDirectoryName(assemblyPath)!);
Assembly assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

foreach (string line in ApiSurfaceFormatter.Format(assembly))
{
    Console.WriteLine(line);
}

return 0;

internal sealed class InspectionLoadContext(string assemblyDirectory)
    : AssemblyLoadContext(isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string candidate = Path.Combine(assemblyDirectory, $"{assemblyName.Name}.dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}

internal static class ApiSurfaceFormatter
{
    private const BindingFlags DeclaredMembers =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    public static IEnumerable<string> Format(Assembly assembly)
    {
        yield return $"assembly {assembly.GetName().Name} {assembly.GetName().Version}";

        foreach (Type type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            yield return FormatTypeHeader(type);

            foreach (string constraint in FormatGenericConstraints(type.GetGenericArguments()))
            {
                yield return $"  {constraint}";
            }

            var members = new List<string>();
            members.AddRange(type.GetConstructors(DeclaredMembers)
                .Where(IsExternallyVisible)
                .Select(FormatConstructor));
            members.AddRange(type.GetFields(DeclaredMembers)
                .Where(IsExternallyVisible)
                .Select(FormatField));
            members.AddRange(type.GetProperties(DeclaredMembers)
                .Where(IsExternallyVisible)
                .Select(FormatProperty));
            members.AddRange(type.GetEvents(DeclaredMembers)
                .Where(IsExternallyVisible)
                .Select(FormatEvent));

            foreach (MethodInfo method in type.GetMethods(DeclaredMembers)
                         .Where(method => !method.IsSpecialName && IsExternallyVisible(method)))
            {
                members.Add(FormatMethod(method));
                members.AddRange(FormatGenericConstraints(method.GetGenericArguments())
                    .Select(constraint => $"  {constraint}"));
            }

            foreach (string member in members.Order(StringComparer.Ordinal))
            {
                yield return $"  {member}";
            }
        }
    }

    private static string FormatTypeHeader(Type type)
    {
        string kind = type.IsInterface
            ? "interface"
            : type.IsEnum
                ? "enum"
                : type.IsValueType
                    ? "struct"
                    : "class";
        var modifiers = new List<string>();
        if (type.IsAbstract && !type.IsInterface)
        {
            modifiers.Add("abstract");
        }
        if (type.IsSealed && !type.IsValueType && !type.IsEnum)
        {
            modifiers.Add("sealed");
        }

        string baseType = type.BaseType is null ? string.Empty : $" base={FormatTypeName(type.BaseType)}";
        string[] interfaces = type.GetInterfaces()
            .Select(FormatTypeName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        string implementedInterfaces = interfaces.Length == 0
            ? string.Empty
            : $" interfaces=[{string.Join(",", interfaces)}]";
        string modifierText = modifiers.Count == 0 ? string.Empty : string.Join(" ", modifiers) + " ";

        return $"type public {modifierText}{kind} {FormatTypeName(type)}{baseType}{implementedInterfaces}";
    }

    private static string FormatConstructor(ConstructorInfo constructor)
    {
        return $"ctor {GetAccess(constructor)} {FormatParameters(constructor.GetParameters())}";
    }

    private static string FormatField(FieldInfo field)
    {
        var modifiers = new List<string> { GetAccess(field) };
        if (field.IsLiteral)
        {
            modifiers.Add("const");
        }
        else
        {
            if (field.IsStatic)
            {
                modifiers.Add("static");
            }
            if (field.IsInitOnly)
            {
                modifiers.Add("readonly");
            }
        }

        string value = field.IsLiteral ? $" = {FormatValue(field.GetRawConstantValue())}" : string.Empty;
        return $"field {string.Join(" ", modifiers)} {FormatTypeName(field.FieldType)} {field.Name}{value}";
    }

    private static string FormatProperty(PropertyInfo property)
    {
        MethodInfo? getter = property.GetGetMethod(nonPublic: true);
        MethodInfo? setter = property.GetSetMethod(nonPublic: true);
        MethodInfo accessor = getter ?? setter
            ?? throw new InvalidOperationException($"Property {property.Name} has no accessors.");
        string staticModifier = accessor.IsStatic ? " static" : string.Empty;
        string indexParameters = property.GetIndexParameters().Length == 0
            ? string.Empty
            : FormatParameters(property.GetIndexParameters());
        string accessors = string.Join(
            ",",
            new[]
            {
                getter is null ? null : $"get={GetAccess(getter)}",
                setter is null ? null : $"set={GetAccess(setter)}"
            }.Where(value => value is not null));

        return $"property{staticModifier} {FormatTypeName(property.PropertyType)} {property.Name}{indexParameters} {{{accessors}}}";
    }

    private static string FormatEvent(EventInfo eventInfo)
    {
        MethodInfo? addMethod = eventInfo.GetAddMethod(nonPublic: true);
        MethodInfo? removeMethod = eventInfo.GetRemoveMethod(nonPublic: true);
        MethodInfo accessor = addMethod ?? removeMethod
            ?? throw new InvalidOperationException($"Event {eventInfo.Name} has no accessors.");
        string staticModifier = accessor.IsStatic ? " static" : string.Empty;
        string accessors = string.Join(
            ",",
            new[]
            {
                addMethod is null ? null : $"add={GetAccess(addMethod)}",
                removeMethod is null ? null : $"remove={GetAccess(removeMethod)}"
            }.Where(value => value is not null));

        return $"event{staticModifier} {FormatTypeName(eventInfo.EventHandlerType!)} {eventInfo.Name} {{{accessors}}}";
    }

    private static string FormatMethod(MethodInfo method)
    {
        var modifiers = new List<string> { GetAccess(method) };
        if (method.IsStatic)
        {
            modifiers.Add("static");
        }
        if (method.IsAbstract)
        {
            modifiers.Add("abstract");
        }
        else if (method.IsVirtual)
        {
            modifiers.Add(method.IsFinal ? "sealed-virtual" : "virtual");
        }

        string genericArguments = method.IsGenericMethodDefinition
            ? $"<{string.Join(",", method.GetGenericArguments().Select(argument => argument.Name))}>"
            : string.Empty;
        string attributes = method.GetCustomAttributesData()
            .Any(attribute => attribute.AttributeType == typeof(System.Diagnostics.DebuggerStepThroughAttribute))
            ? " [DebuggerStepThrough]"
            : string.Empty;

        return $"method {string.Join(" ", modifiers)} {FormatTypeName(method.ReturnType)} "
            + $"{method.Name}{genericArguments}{FormatParameters(method.GetParameters())}{attributes}";
    }

    private static string FormatParameters(IReadOnlyList<ParameterInfo> parameters)
    {
        return $"({string.Join(", ", parameters.Select(FormatParameter))})";
    }

    private static string FormatParameter(ParameterInfo parameter)
    {
        Type parameterType = parameter.ParameterType;
        string modifier = string.Empty;
        if (parameter.GetCustomAttributesData().Any(attribute => attribute.AttributeType == typeof(ParamArrayAttribute)))
        {
            modifier = "params ";
        }
        else if (parameterType.IsByRef)
        {
            modifier = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
            parameterType = parameterType.GetElementType()!;
        }

        string callerMemberName = parameter.GetCustomAttributesData()
            .Any(attribute => attribute.AttributeType == typeof(CallerMemberNameAttribute))
            ? "[CallerMemberName] "
            : string.Empty;
        string defaultValue = parameter.HasDefaultValue
            ? $" = {FormatValue(parameter.DefaultValue)}"
            : string.Empty;

        return $"{callerMemberName}{modifier}{FormatTypeName(parameterType)} {parameter.Name}{defaultValue}";
    }

    private static IEnumerable<string> FormatGenericConstraints(IEnumerable<Type> genericArguments)
    {
        foreach (Type argument in genericArguments.Where(argument => argument.IsGenericParameter))
        {
            var constraints = new List<string>();
            GenericParameterAttributes attributes =
                argument.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
            if (attributes.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint))
            {
                constraints.Add("class");
            }
            if (attributes.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
            {
                constraints.Add("struct");
            }

            constraints.AddRange(argument.GetGenericParameterConstraints().Select(FormatTypeName));

            if (attributes.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint))
            {
                constraints.Add("new()");
            }

            if (constraints.Count > 0)
            {
                yield return $"constraint {argument.Name} : {string.Join(",", constraints)}";
            }
        }
    }

    private static string FormatTypeName(Type type)
    {
        if (type.IsByRef)
        {
            return FormatTypeName(type.GetElementType()!) + "&";
        }
        if (type.IsPointer)
        {
            return FormatTypeName(type.GetElementType()!) + "*";
        }
        if (type.IsArray)
        {
            return FormatTypeName(type.GetElementType()!) + "[]";
        }
        if (type.IsGenericParameter)
        {
            return type.Name;
        }
        if (!type.IsGenericType)
        {
            return type.FullName ?? type.Name;
        }

        Type definition = type.GetGenericTypeDefinition();
        string name = definition.FullName ?? definition.Name;
        int tick = name.IndexOf('`');
        if (tick >= 0)
        {
            name = name[..tick];
        }

        return $"{name}<{string.Join(",", type.GetGenericArguments().Select(FormatTypeName))}>";
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            string text => $"\"{text.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"",
            char character => $"'{character}'",
            bool boolean => boolean ? "true" : "false",
            _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static bool IsExternallyVisible(MethodBase method)
    {
        return method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;
    }

    private static bool IsExternallyVisible(FieldInfo field)
    {
        return field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly;
    }

    private static bool IsExternallyVisible(PropertyInfo property)
    {
        return property.GetAccessors(nonPublic: true).Any(IsExternallyVisible);
    }

    private static bool IsExternallyVisible(EventInfo eventInfo)
    {
        MethodInfo? addMethod = eventInfo.GetAddMethod(nonPublic: true);
        MethodInfo? removeMethod = eventInfo.GetRemoveMethod(nonPublic: true);
        return (addMethod is not null && IsExternallyVisible(addMethod))
            || (removeMethod is not null && IsExternallyVisible(removeMethod));
    }

    private static string GetAccess(MethodBase method)
    {
        if (method.IsPublic)
        {
            return "public";
        }
        if (method.IsFamilyOrAssembly)
        {
            return "protected-internal";
        }
        if (method.IsFamily)
        {
            return "protected";
        }

        return "nonpublic";
    }

    private static string GetAccess(FieldInfo field)
    {
        if (field.IsPublic)
        {
            return "public";
        }
        if (field.IsFamilyOrAssembly)
        {
            return "protected-internal";
        }
        if (field.IsFamily)
        {
            return "protected";
        }

        return "nonpublic";
    }
}
