using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Resolve the packaged DLL's framework imports against the actual Windows
// framework, rather than Mono's more permissive implementation assemblies.
internal static class FrameworkTests
{
    public static int Main(string[] args)
    {
        if (args[0] == "--write-incompatible")
        {
            // Emit the bad import directly: this fixture must work even when
            // the host compiler itself does not expose the incompatible API.
            using (var fixture = ModuleDefinition.CreateModule("Incompatible", ModuleKind.Dll))
            {
                var type = new TypeDefinition("", "Incompatible", TypeAttributes.Public, fixture.TypeSystem.Object);
                fixture.Types.Add(type);
                var test = new MethodDefinition("Test", MethodAttributes.Public | MethodAttributes.Static, fixture.TypeSystem.Void);
                type.Methods.Add(test);
                var split = new MethodReference("Split", new ArrayType(fixture.TypeSystem.String), fixture.TypeSystem.String) { HasThis = true };
                split.Parameters.Add(new ParameterDefinition(fixture.TypeSystem.Char));
                split.Parameters.Add(new ParameterDefinition(fixture.ImportReference(typeof(StringSplitOptions))));
                var il = test.Body.GetILProcessor();
                il.Emit(OpCodes.Ldstr, "test");
                il.Emit(OpCodes.Ldc_I4_S, (sbyte)32);
                il.Emit(OpCodes.Ldc_I4_0);
                il.Emit(OpCodes.Callvirt, split);
                il.Emit(OpCodes.Pop);
                il.Emit(OpCodes.Ret);
                fixture.Write(args[1]);
            }
            return 0;
        }
        var resolver = new DefaultAssemblyResolver();
        foreach (string directory in resolver.GetSearchDirectories()) resolver.RemoveSearchDirectory(directory);
        resolver.AddSearchDirectory(args[1]);
        int checkedMethods = 0, failures = 0;
        using (var module = ModuleDefinition.ReadModule(args[0], new ReaderParameters { AssemblyResolver = resolver }))
        {
            foreach (var member in module.GetMemberReferences())
            {
                var scope = member.DeclaringType.Scope as AssemblyNameReference;
                if (scope == null || (scope.Name != "mscorlib" && scope.Name != "System" && scope.Name != "System.Core")) continue;
                var method = member as MethodReference;
                if (method == null) continue;
                checkedMethods++;
                try
                {
                    if (method.Resolve() == null) { Console.WriteLine("UNRESOLVED: " + method.FullName); failures++; }
                }
                catch (Exception error) { Console.WriteLine("UNRESOLVED: " + method.FullName + ": " + error.Message); failures++; }
            }
        }
        Console.WriteLine("Framework imports: " + checkedMethods + " checked, " + failures + " unresolved");
        return failures == 0 && checkedMethods > 0 ? 0 : 1;
    }
}
