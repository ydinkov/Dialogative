
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace Dialogative.helpers
{
    internal static class Eval
    {
        internal static async Task<bool> BoolAsync(this string? query, Func<ICollection<string>> _declarations,Func<ICollection<string>> mutations) => 
            await CSharpScript.EvaluateAsync<bool>(GetScript(query, _declarations, mutations));

        internal static bool Bool(this string? query, Func<ICollection<string>> _declarations,Func<ICollection<string>> mutations) => 
            CSharpScript.EvaluateAsync<bool>(GetScript(query, _declarations, mutations), ScriptOptions.Default).GetAwaiter().GetResult();

        private static string GetScript(string? query, Func<ICollection<string>> _declarations,Func<ICollection<string>> mutations)
        {
            var declarations = _declarations();
            var decl = declarations.Select(x => $"bool {x};\n");
            var mut = mutations().Where(x=>declarations.Any(x.Contains)).Select(x => $"{x};\n").ToList();
            var scriptLines = new List<string>();
            scriptLines.AddRange(decl);
            scriptLines.AddRange(mut);
            scriptLines.Add($"return {query};");
            return string.Join("", scriptLines);
        }
    }
}