using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Aurora.Tests;

internal static class LegacyTypeContract
{
    // Read metadata only; never execute the legacy fixture or load it into the
    // application's assembly context. The fixture also travels with oracle runs.
    public static void AssertRequiredTypes(Assembly assembly)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "LegacyOracles", assembly.GetName().Name + ".dll");
        using var stream = File.OpenRead(path);
        using var pe = new PEReader(stream);
        MetadataReader metadata = pe.GetMetadataReader();

        bool IsExported(TypeDefinitionHandle handle)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;
            return visibility == TypeAttributes.Public ||
                (visibility == TypeAttributes.NestedPublic && IsExported(type.GetDeclaringType()));
        }

        string FullName(TypeDefinitionHandle handle)
        {
            TypeDefinition type = metadata.GetTypeDefinition(handle);
            string name = metadata.GetString(type.Name);
            if (type.IsNested)
                return FullName(type.GetDeclaringType()) + "+" + name;
            string ns = metadata.GetString(type.Namespace);
            return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }

        string[] required = metadata.TypeDefinitions.Where(IsExported).Select(FullName).ToArray();
        required.Should().NotBeEmpty();
        assembly.GetExportedTypes().Select(type => type.FullName)
            .Should().Contain(required, "all exported legacy types must remain available; additions are allowed");
    }
}
