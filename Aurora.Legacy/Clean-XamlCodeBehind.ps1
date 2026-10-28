param(
    [string]$RepoRoot = "."
)

$files = Get-ChildItem -Path $RepoRoot -Recurse -Filter "*.cs" | Where-Object {
    $c = Get-Content $_.FullName -Raw
    # Only touch files that have the XAML-generated boilerplate
    $c -match 'private bool _contentLoaded' -or
    $c -match '\[GeneratedCode\("PresentationBuildTasks"' -or
    $c -match 'internal Delegate _CreateDelegate'
}

Write-Host "Found $($files.Count) files to clean."

foreach ($file in $files) {
    $lines = Get-Content $file.FullName
    $output = [System.Collections.Generic.List[string]]::new()
    $skip = $false
    $braceDepth = 0
    $skipStartDepth = 0
    $i = 0

    while ($i -lt $lines.Count) {
        $line = $lines[$i]

        if (-not $skip) {
            # Detect start of a block to remove
            $shouldSkip = $false

            # Pattern 1: [GeneratedCode("PresentationBuildTasks"...)] on previous or this line
            # We look for the method signatures that follow it
            if ($line -match '^\s*\[GeneratedCode\("PresentationBuildTasks"') {
                # Peek ahead to find the method this decorates
                $j = $i + 1
                while ($j -lt $lines.Count -and $lines[$j] -match '^\s*\[') { $j++ }
                $methodLine = if ($j -lt $lines.Count) { $lines[$j] } else { "" }
                if ($methodLine -match 'void InitializeComponent\(\)' -or
                    $methodLine -match 'internal Delegate _CreateDelegate' -or
                    $methodLine -match 'void IComponentConnector\.Connect' -or
                    $methodLine -match 'void IStyleConnector\.Connect' -or
                    $methodLine -match 'static void Main\(' -or
                    $methodLine -match 'public static void Main\(') {
                    $shouldSkip = $true
                }
            }

            # Pattern 2: [DebuggerNonUserCode] line followed by GeneratedCode and then one of our methods
            if ($line -match '^\s*\[DebuggerNonUserCode\]') {
                $j = $i + 1
                while ($j -lt $lines.Count -and $lines[$j] -match '^\s*\[') { $j++ }
                $methodLine = if ($j -lt $lines.Count) { $lines[$j] } else { "" }
                if ($methodLine -match 'void InitializeComponent\(\)' -or
                    $methodLine -match 'internal Delegate _CreateDelegate' -or
                    $methodLine -match 'void IComponentConnector\.Connect' -or
                    $methodLine -match 'void IStyleConnector\.Connect') {
                    $shouldSkip = $true
                }
            }

            # Pattern 3: bare method signatures without attributes (some files omit attributes)
            if ($line -match '^\s*void IComponentConnector\.Connect\(int connectionId, object target\)' -or
                $line -match '^\s*void IStyleConnector\.Connect\(int connectionId, object target') {
                $shouldSkip = $true
            }

            # Pattern 4: private bool _contentLoaded; (single line field)
            if ($line -match '^\s*private bool _contentLoaded;\s*$') {
                $i++
                continue  # skip this line, don't add to output
            }

            # Pattern 5: internal field declarations (XAML named elements)
            # These are single-line: internal <Type> <Name>;
            if ($line -match '^\s*internal\s+\S+\s+\w+;\s*$') {
                $i++
                continue
            }

            # Pattern 6: internal Delegate _CreateDelegate (single line check before block removal)
            if ($line -match '^\s*internal Delegate _CreateDelegate') {
                $shouldSkip = $true
            }

            if ($shouldSkip) {
                # Find the opening brace and skip until matching close brace
                $skip = $true
                $braceDepth = 0
                $skipStartDepth = 0
                # Don't add current line
                $i++
                continue
            }

            $output.Add($line)
        }
        else {
            # We're inside a block we're skipping — count braces
            $braceDepth += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
            $braceDepth -= ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

            if ($braceDepth -le 0 -and ($line -match '\{' -or $braceDepth -lt 0)) {
                # Check if this line closes the block
                $opens = ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
                $closes = ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count
                if ($closes -gt 0 -and $braceDepth -le 0) {
                    $skip = $false
                    $braceDepth = 0
                }
            }
            elseif ($braceDepth -le 0 -and $line -match '^\s*(=>|{)') {
                # Single-line method body like: => this._contentLoaded = true;
                $skip = $false
                $braceDepth = 0
            }
        }

        $i++
    }

    # Also remove IComponentConnector and IStyleConnector from class declaration
    $result = ($output -join "`n")
    $result = $result -replace ',\s*\r?\n?\s*IComponentConnector\b', ''
    $result = $result -replace ',\s*\r?\n?\s*IStyleConnector\b', ''
    $result = $result -replace ',\s*IComponentConnector\b', ''
    $result = $result -replace ',\s*IStyleConnector\b', ''

    Set-Content -Path $file.FullName -Value $result -NoNewline
    Write-Host "Cleaned: $($file.Name)"
}

Write-Host "`nDone. Run dotnet build to verify."
