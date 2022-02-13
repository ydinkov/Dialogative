
using Microsoft.CodeAnalysis.CSharp.Scripting;

namespace Dialogative.helpers
{
    internal static class Eval
    {
        internal static async Task<bool> Bool(this string? query, Func<ICollection<string>> _declarations,Func<ICollection<string>> mutations)
        {
            if (string.IsNullOrWhiteSpace(query)) return false;
            var declarations = _declarations();
            var decl = declarations.Select(x => $"bool {x};\n");
            var mut = mutations().Where(x=>declarations.Any(x.Contains)).Select(x => $"{x};\n").ToList();
            var scriptLines = new List<string>();
            scriptLines.AddRange(decl);
            scriptLines.AddRange(mut);
            scriptLines.Add($"return {query};");
            
            var script = string.Join("", scriptLines);
            return await CSharpScript.EvaluateAsync<bool>(script);
        }
    }
}