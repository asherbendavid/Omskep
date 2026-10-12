<#
.SYNOPSIS
    Lists every Public/Friend/Protected/Private Class, Module, Interface, Enum, Structure,
    Function, Sub and Property declaration in a VB.NET solution, grouped by file.

.DESCRIPTION
    Run from anywhere; point it at a solution root with -Path, or run it from that root with
    no arguments. Writes a flat "relativepath:line: declaration" listing to an output file.
    Edit $excludeDirs / $excludeFiles below for any project-specific folders (e.g. a throwaway
    spike/ folder) you don't want included.

.PARAMETER Path
    Solution root to scan. Defaults to the current directory.

.PARAMETER OutFile
    Where to write the listing. Defaults to declarations.txt in the current directory.

.EXAMPLE
    .\Get-VbDeclarations.ps1
    .\Get-VbDeclarations.ps1 -Path C:\source\SomeOtherProject -OutFile other-decls.txt
#>

param(
    [string]$Path = ".",
    [string]$OutFile = "declarations.txt"
)

# Folders to skip anywhere in the tree (matched as \name\ segments).
$excludeDirs = @('bin', 'obj', 'spike', '.vs', '.git')

# Specific file name patterns to skip (e.g. designer files, which are machine-generated).
$excludeFiles = @('*.Designer.vb')

$pattern = '^\s*(Public|Friend|Protected|Private)\s+' +
           '(NotInheritable\s+|MustInherit\s+|Shared\s+|ReadOnly\s+|Overridable\s+|Overrides\s+|Partial\s+)*' +
           '(Class|Module|Interface|Enum|Structure|Function|Sub|Property)\b'

$root = (Resolve-Path $Path).Path

$files = Get-ChildItem -Path $root -Filter *.vb -Recurse |
    Where-Object {
        $full = $_.FullName
        -not ($excludeDirs | Where-Object { $full -match [regex]::Escape("\$_\") })
    } |
    Where-Object {
        $name = $_.Name
        -not ($excludeFiles | Where-Object { $name -like $_ })
    }

$lines = foreach ($file in $files) {
    $relative = $file.FullName.Substring($root.Length).TrimStart('\', '/')
    Select-String -Path $file.FullName -Pattern $pattern |
        ForEach-Object { "$relative`:$($_.LineNumber): $($_.Line.Trim())" }
}

$lines | Out-File -FilePath $OutFile -Encoding utf8
Write-Host "Wrote $($lines.Count) declarations from $($files.Count) files to $OutFile"
