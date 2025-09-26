#if NET6_0_OR_GREATER
using System;
using System.Linq;
using System.Reflection;
using AsmResolver;
using AsmResolver.DotNet;
using HarmonyLib;
using Il2CppInterop.Generator.Contexts;

namespace MelonLoader.Fixes.AsmResolver
{
    // Herp: This fixes an OOBE issue with AsmResolver Utf8String.Concat and its usage inside Il2CppInterop's MethodRewriteContext.UnmangleMethodNameWithSignature
    // AsmResolver does not have extended validation of UTF-8 encoded strings
    // As such it fails when it runs into a null or empty string as either parameter of Utf8String.Concat and Utf8String's Concatenate Operator
    // This also patches MethodRewriteContext.UnmangleMethodNameWithSignature and redirects it to a fixed variant with extended validation implemented
    internal class AsmResolverUtf8StringConcatFix
    {
        private static MethodInfo _concat;
        private static MethodInfo _concatPrefix;

        private static MethodInfo _produceMethodSignatureBase;
        private static MethodInfo _parameterSignatureMatchesThis;
        
        private static MethodInfo _unmangleMethodNameWithSignature;
        private static MethodInfo _unmangleMethodNameWithSignaturePrefix;

        internal static void Install()
        {
            try
            {
                Type thisType = typeof(AsmResolverUtf8StringConcatFix);
                Type contextType = typeof(MethodRewriteContext);
                Type utf8StringType = typeof(Utf8String);

                _concat = utf8StringType.GetMethod("Concat", BindingFlags.Public | BindingFlags.Instance, [typeof(byte[])]);
                if (_concat == null)
                    throw new Exception("Failed to get Utf8String.Concat(byte[])");

                _produceMethodSignatureBase = contextType.GetMethod("ProduceMethodSignatureBase", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_produceMethodSignatureBase == null)
                    throw new Exception("Failed to get MethodRewriteContext.ProduceMethodSignatureBase");

                _parameterSignatureMatchesThis = contextType.GetMethod("ParameterSignatureMatchesThis", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_parameterSignatureMatchesThis == null)
                    throw new Exception("Failed to get MethodRewriteContext.ParameterSignatureMatchesThis");

                _unmangleMethodNameWithSignature = contextType.GetMethod("UnmangleMethodNameWithSignature", BindingFlags.NonPublic | BindingFlags.Instance);
                if (_unmangleMethodNameWithSignature == null)
                    throw new Exception("Failed to get MethodRewriteContext.UnmangleMethodNameWithSignature");

                _concatPrefix = thisType.GetMethod("ConcatPrefix", BindingFlags.NonPublic | BindingFlags.Static);
                if (_concatPrefix == null)
                    throw new Exception("Failed to get AsmResolverUtf8StringConcatFix.ConcatPrefix");

                _unmangleMethodNameWithSignaturePrefix = thisType.GetMethod("UnmangleMethodNameWithSignaturePrefix", BindingFlags.NonPublic | BindingFlags.Static);
                if (_unmangleMethodNameWithSignaturePrefix == null)
                    throw new Exception("Failed to get AsmResolverUtf8StringConcatFix.UnmangleMethodNameWithSignaturePrefix");

                MelonDebug.Msg($"Patching AsmResolver Utf8String.Concat(byte[])...");
                Core.HarmonyInstance.Patch(_concat,
                    new HarmonyMethod(_concatPrefix));

                MelonDebug.Msg($"Patching Il2CppInterop MethodRewriteContext.UnmangleMethodNameWithSignature...");
                Core.HarmonyInstance.Patch(_unmangleMethodNameWithSignature,
                    new HarmonyMethod(_unmangleMethodNameWithSignaturePrefix));
            }
            catch (Exception e)
            {
                MelonLogger.Warning(e);
            }
        }

        private static bool ConcatPrefix(Utf8String __instance, byte[] __0, ref Utf8String __result)
        {
            __result = ConcatFixed(__instance, __0);
            return false;
        }

        private static bool UnmangleMethodNameWithSignaturePrefix(MethodRewriteContext __instance, ref string __result)
        {
            string baseSig = (string)_produceMethodSignatureBase.Invoke(__instance, []);

            int methodCount = 0;
            var allMethods = __instance.DeclaringType.Methods;
            if (allMethods.Count() > 0)
            {
                var matchesFound = allMethods.Where((x) => (bool)_parameterSignatureMatchesThis.Invoke(__instance, [x]));
                if (matchesFound.Count() > 0)
                {
                    matchesFound = matchesFound.TakeWhile(it => it != __instance);
                    methodCount = matchesFound.Count();
                }
            }

            var unmangleMethodNameWithSignature = $"{baseSig}_{methodCount}";

            if (__instance.DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                    CombineStringsFixed($"{GetNamespacePrefixFixed(__instance.DeclaringType.NewType)}.", 
                    CombineStringsFixed(__instance.DeclaringType.NewType.Name, "::")) + unmangleMethodNameWithSignature, out var newNameByType))
            {
                unmangleMethodNameWithSignature = newNameByType;
            }
            else if (__instance.DeclaringType.AssemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                    GetNamespacePrefixFixed(__instance.DeclaringType.NewType) + "::" + unmangleMethodNameWithSignature, out var newName))
            {
                unmangleMethodNameWithSignature = newName;
            }

            __result = unmangleMethodNameWithSignature;
            return false;
        }

        private static string GetNamespacePrefixFixed(ITypeDefOrRef type)
        {
            if (type.DeclaringType is not null)
                return CombineStringsFixed($"{GetNamespacePrefixFixed(type.DeclaringType)}.", type.DeclaringType.Name);

            return type.Namespace;
        }

        private static string CombineStringsFixed(string a, Utf8String b)
        {
            if (string.IsNullOrEmpty(a))
                return string.Empty;

            if (Utf8String.IsNullOrEmpty(b))
                return a!;

            return ConcatFixed(a, b);
        }

        private static Utf8String CombineStringsFixed(Utf8String a, string b)
        {
            if (string.IsNullOrEmpty(b))
                return Utf8String.Empty;

            if (Utf8String.IsNullOrEmpty(a))
                return b!;

            return ConcatFixed(a, b);
        }

        private static Utf8String ConcatFixed(Utf8String a, Utf8String b) => !Utf8String.IsNullOrEmpty(b)
                ? ConcatFixed(a, b.GetBytes())
                : a;
        private static Utf8String ConcatFixed(Utf8String a, byte[] b)
        {
            if (b is null || b.Length == 0)
                return a;

            var aBytes = a.GetBytes();
            byte[] result = new byte[aBytes.Length + b.Length];
            Buffer.BlockCopy(aBytes, 0, result, 0, aBytes.Length);
            Buffer.BlockCopy(b, 0, result, aBytes.Length, b.Length);
            return result;
        }
    }
}
#endif