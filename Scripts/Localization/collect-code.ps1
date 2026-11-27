# Use the compiler's syntax tree to distinguish display strings from comments, attributes and code inside interpolation.
param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference='Stop'
$roslyn=$PSHOME
Add-Type -Path (Join-Path $roslyn Microsoft.CodeAnalysis.dll)
Add-Type -Path (Join-Path $roslyn Microsoft.CodeAnalysis.CSharp.dll)
$rows = [System.Collections.Generic.List[object]]::new()
$files = rg --files Assets/Scripts Assets/_ThirdParty -g '*.cs' -g '!**/Editor/**' -g '!**/*Tests*/**' -g '!**/Localization/**'
foreach ($path in $files) {
    $path=$path.Replace('\','/')
    if ($path -match '/_ThirdParty/' -and $path -notmatch '/_ThirdParty/(curtaincall[^/]*/|EscapeResultsKit/)') { continue }
    $code=[IO.File]::ReadAllText((Resolve-Path -LiteralPath $path))
    $tree=[Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($code)
    foreach ($node in $tree.GetRoot().DescendantNodes()) {
        if ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.LiteralExpressionSyntax] -and $node.Token.Value -is [string]) {
            $value=$node.Token.ValueText
        } elseif ($node -is [Microsoft.CodeAnalysis.CSharp.Syntax.InterpolatedStringExpressionSyntax]) {
            $index=0
            $value=-join @($node.Contents | ForEach-Object {
                if ($_ -is [Microsoft.CodeAnalysis.CSharp.Syntax.InterpolationSyntax]) { '{'+$index+'}'; $index++ }
                else { $_.TextToken.ValueText }
            })
        } else {continue}
        if ([string]::IsNullOrWhiteSpace($value)) { continue }
        if ($value -notmatch '[가-힣]' -and !$value.StartsWith('@loc:', [StringComparison]::Ordinal)) {
            # English-only literals written to text components, including SetText calls.
            if ($node.Parent.ToString() -cnotmatch '(\.text\s*=|\.SetText\s*\()' -or $value -notmatch '[a-zA-Z]{2}') { continue }
        }
        $reject=$false
        foreach ($ancestor in $node.Ancestors()) {
            if ($ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.AttributeSyntax] -or $ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.ThrowStatementSyntax]) { $reject=$true;break }
            if ($ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax] -and $ancestor.Expression.ToString() -match 'Debug\.Log|Gizmos\.|FindProperty|Profiler\.') { $reject=$true;break }
            if ($ancestor -is [Microsoft.CodeAnalysis.CSharp.Syntax.MemberDeclarationSyntax]) {break}
        }
        if (!$reject) {
            $line=$tree.GetLineSpan($node.Span).StartLinePosition.Line+1
            $rows.Add([PSCustomObject]@{asset=$path;location=$line;source=$value})
        }
    }
}
$rows | Export-Csv -NoTypeInformation -Encoding utf8 -LiteralPath $OutputPath

