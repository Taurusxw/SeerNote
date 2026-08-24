[CmdletBinding()]
param(
    [ValidateSet('Build', 'Test', 'Release', 'Verify', 'Clean')]
    [string]$Task = 'Build'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$artifactRoot = Join-Path $projectRoot 'artifacts'
$buildRoot = Join-Path $artifactRoot 'build'
$testRoot = Join-Path $artifactRoot 'tests'
$sourceRoot = Join-Path $projectRoot 'src\SeerNote'
$cliSourceRoot = Join-Path $projectRoot 'src\SeerNote.Cli'
$testSourceRoot = Join-Path $projectRoot 'tests\SeerNote.Tests'
$iconPath = Join-Path $projectRoot 'SeerNote.ico'
$manifestPath = Join-Path $sourceRoot 'app.manifest'
$releaseExe = Join-Path $projectRoot 'SeerNote.exe'
$releaseCliExe = Join-Path $projectRoot 'SeerNote.Cli.exe'
$fontSourceDirectory = Join-Path $projectRoot 'assets\fonts'
$fontFileName = 'SourceHanSansCN-Regular.otf'
$fontLicenseFileName = 'OFL-SourceHanSans.txt'
$fontSourcePath = Join-Path $fontSourceDirectory $fontFileName
$fontLicenseSourcePath = Join-Path $fontSourceDirectory $fontLicenseFileName

function Find-CSharpCompiler {
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path -LiteralPath $vswhere)) {
        throw '未找到 Visual Studio Installer 的 vswhere.exe。'
    }

    $install = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
    if (-not $install) {
        throw '未找到 Visual Studio 2022 Build Tools。'
    }

    $compiler = Join-Path $install 'MSBuild\Current\Bin\Roslyn\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        throw "未找到 Roslyn 编译器：$compiler"
    }
    return $compiler
}

function Find-FrameworkReference([string]$AssemblyName, [string]$GacKind = 'GAC_MSIL') {
    $gacRoot = Join-Path 'C:\Windows\Microsoft.NET\assembly' "$GacKind\$AssemblyName"
    $candidate = Get-ChildItem -LiteralPath $gacRoot -Filter "$AssemblyName.dll" -File -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending |
        Select-Object -First 1 -ExpandProperty FullName
    if (-not $candidate) {
        $frameworkPath = Join-Path 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319' "$AssemblyName.dll"
        if (Test-Path -LiteralPath $frameworkPath) {
            return $frameworkPath
        }
        throw "未找到 .NET Framework 程序集：$AssemblyName"
    }
    return $candidate
}

function Get-References {
    @(
        (Find-FrameworkReference 'mscorlib'),
        (Find-FrameworkReference 'System'),
        (Find-FrameworkReference 'System.Core'),
        (Find-FrameworkReference 'System.Xaml'),
        (Find-FrameworkReference 'System.Runtime.Serialization'),
        (Find-FrameworkReference 'System.Windows.Forms'),
        (Find-FrameworkReference 'System.Drawing'),
        (Find-FrameworkReference 'WindowsBase'),
        (Find-FrameworkReference 'PresentationCore' 'GAC_64'),
        (Find-FrameworkReference 'PresentationFramework')
    )
}

function Ensure-Icon {
    if (-not (Test-Path -LiteralPath $iconPath)) {
        & (Join-Path $projectRoot 'tools\New-SeerNoteIcon.ps1') -OutputPath $iconPath
    }
}

function Copy-AppAssets([string]$DestinationRoot) {
    if (-not (Test-Path -LiteralPath $fontSourcePath -PathType Leaf)) {
        throw "缺少应用私有字体：$fontSourcePath"
    }
    if (-not (Test-Path -LiteralPath $fontLicenseSourcePath -PathType Leaf)) {
        throw "缺少字体许可证：$fontLicenseSourcePath"
    }

    $resolvedDestination = [IO.Path]::GetFullPath($DestinationRoot)
    $resolvedRoot = [IO.Path]::GetFullPath($projectRoot)
    $rootPrefix = $resolvedRoot + [IO.Path]::DirectorySeparatorChar
    $isProjectRoot = $resolvedDestination.Equals($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)
    $isProjectChild = $resolvedDestination.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)
    if (-not $isProjectRoot -and -not $isProjectChild) {
        throw "拒绝把应用资产复制到项目目录之外：$resolvedDestination"
    }

    $fontDestination = Join-Path $resolvedDestination 'fonts'
    New-Item -ItemType Directory -Force -Path $fontDestination | Out-Null
    Copy-Item -LiteralPath $fontSourcePath -Destination (Join-Path $fontDestination $fontFileName) -Force
    Copy-Item -LiteralPath $fontLicenseSourcePath -Destination (Join-Path $fontDestination $fontLicenseFileName) -Force
}

function Build-App {
    New-Item -ItemType Directory -Force -Path $buildRoot | Out-Null
    Ensure-Icon
    $compiler = Find-CSharpCompiler
    $sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName
    if (-not $sources) {
        throw '没有找到 SeerNote C# 源文件。'
    }
    $output = Join-Path $buildRoot 'SeerNote.exe'
    $arguments = @(
        '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/deterministic+', '/utf8output',
        '/langversion:latest', "/out:$output", "/win32icon:$iconPath", "/win32manifest:$manifestPath",
        '/main:SeerNote.App'
    )
    $arguments += Get-References | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "SeerNote 编译失败，退出码 $LASTEXITCODE。"
    }
    Copy-AppAssets $buildRoot
    return $output
}

function Build-Cli([string]$appAssembly) {
    $compiler = Find-CSharpCompiler
    $sources = Get-ChildItem -LiteralPath $cliSourceRoot -Filter '*.cs' -File -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName
    if (-not $sources) {
        throw '没有找到 SeerNote CLI C# 源文件。'
    }
    $output = Join-Path $buildRoot 'SeerNote.Cli.exe'
    $arguments = @(
        '/nologo', '/target:exe', '/platform:anycpu', '/optimize+', '/deterministic+', '/utf8output',
        '/langversion:latest', "/out:$output", "/win32icon:$iconPath", '/main:SeerNote.Cli.Program',
        "/reference:$appAssembly"
    )
    $arguments += Get-References | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "SeerNote CLI 编译失败，退出码 $LASTEXITCODE。"
    }
    return $output
}

function Build-And-Run-Tests([string]$appAssembly, [string]$cliAssembly) {
    New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
    $testAppAssembly = Join-Path $testRoot 'SeerNote.exe'
    $testCliAssembly = Join-Path $testRoot 'SeerNote.Cli.exe'
    Copy-Item -LiteralPath $appAssembly -Destination $testAppAssembly -Force
    Copy-Item -LiteralPath $cliAssembly -Destination $testCliAssembly -Force
    Copy-AppAssets $testRoot
    $compiler = Find-CSharpCompiler
    $sources = Get-ChildItem -LiteralPath $testSourceRoot -Filter '*.cs' -File -Recurse | Sort-Object FullName | Select-Object -ExpandProperty FullName
    if (-not $sources) {
        throw '没有找到 SeerNote 测试源文件。'
    }
    $testExe = Join-Path $testRoot 'SeerNote.Tests.exe'
    $arguments = @('/nologo', '/target:exe', '/platform:anycpu', '/optimize+', '/deterministic+', '/utf8output', '/langversion:latest', "/out:$testExe", "/reference:$testAppAssembly")
    $arguments += Get-References | ForEach-Object { "/reference:$_" }
    $arguments += $sources
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "测试程序编译失败，退出码 $LASTEXITCODE。"
    }
    & $testExe
    if ($LASTEXITCODE -ne 0) {
        throw "测试失败，退出码 $LASTEXITCODE。"
    }
}

function Publish-App([string]$appAssembly, [string]$cliAssembly) {
    Copy-Item -LiteralPath $appAssembly -Destination $releaseExe -Force
    Copy-Item -LiteralPath $cliAssembly -Destination $releaseCliExe -Force
    Copy-AppAssets $projectRoot
    $appSize = (Get-Item -LiteralPath $releaseExe).Length
    $cliSize = (Get-Item -LiteralPath $releaseCliExe).Length
    if ($appSize -ge 5MB) {
        throw "桌面程序超过 5 MiB：$appSize bytes"
    }
    if ($cliSize -ge 5MB) {
        throw "CLI 程序超过 5 MiB：$cliSize bytes"
    }
    $fontSize = (Get-Item -LiteralPath (Join-Path $projectRoot "fonts\$fontFileName")).Length
    $licenseSize = (Get-Item -LiteralPath (Join-Path $projectRoot "fonts\$fontLicenseFileName")).Length
    $distributionSize = $appSize + $cliSize + $fontSize + $licenseSize
    if ($distributionSize -ge 10MB) {
        throw "发布程序与必要字体资产合计超过 10 MiB：$distributionSize bytes"
    }
    Write-Host "Published: $releaseExe ($appSize bytes), $releaseCliExe ($cliSize bytes); portable distribution $distributionSize bytes"
}

function Verify-PublishStructure {
    if (-not (Test-Path -LiteralPath $releaseExe -PathType Leaf)) {
        throw '根目录缺少 SeerNote.exe。'
    }
    if (-not (Test-Path -LiteralPath $releaseCliExe -PathType Leaf)) {
        throw '根目录缺少 SeerNote.Cli.exe。'
    }
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw '根目录缺少 SeerNote.ico。'
    }

    $publishedFont = Join-Path $projectRoot "fonts\$fontFileName"
    $publishedFontLicense = Join-Path $projectRoot "fonts\$fontLicenseFileName"
    if (-not (Test-Path -LiteralPath $publishedFont -PathType Leaf)) {
        throw "根目录缺少应用私有字体：$publishedFont"
    }
    if (-not (Test-Path -LiteralPath $publishedFontLicense -PathType Leaf)) {
        throw "根目录缺少字体许可证：$publishedFontLicense"
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $publishedFont).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $fontSourcePath).Hash) {
        throw '发布字体与受控源文件不一致。'
    }
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $publishedFontLicense).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $fontLicenseSourcePath).Hash) {
        throw '发布字体许可证与受控源文件不一致。'
    }

    $unexpectedDlls = @(Get-ChildItem -LiteralPath $projectRoot -Filter '*.dll' -File)
    if ($unexpectedDlls.Count -gt 0) {
        throw "根发布目录包含不应存在的 DLL：$($unexpectedDlls.Name -join ', ')"
    }

    Add-Type -AssemblyName System.Drawing
    $embeddedIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($releaseExe)
    if ($null -eq $embeddedIcon -or $embeddedIcon.Width -le 0 -or $embeddedIcon.Height -le 0) {
        throw 'SeerNote.exe 未能读取嵌入图标。'
    }
    $embeddedIcon.Dispose()

    $sourceFiles = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cs' -File -Recurse
    $forbidden = $sourceFiles | Select-String -Pattern 'Microsoft\.Win32\.Registry|RegistryKey|System\.Net\.Http|HttpClient|WebClient|HttpWebRequest|TcpClient|UdpClient' -CaseSensitive
    if ($forbidden) {
        $forbiddenPaths = ($forbidden.Path | Select-Object -Unique) -join ', '
        throw "检测到超出首版隐私边界的注册表或网络 API：$forbiddenPaths"
    }

    $schemaOutput = @(& $releaseCliExe schema)
    if ($LASTEXITCODE -ne 0 -or $schemaOutput.Count -ne 1) {
        throw 'SeerNote.Cli.exe schema 未返回单行成功结果。'
    }
    $schemaEnvelope = $schemaOutput[0] | ConvertFrom-Json
    if (-not $schemaEnvelope.ok -or $schemaEnvelope.contract -ne 'seernote.cli.v1' -or $schemaEnvelope.data.schema.noteContract -ne 'seernote.note.v1') {
        throw 'SeerNote.Cli.exe schema 契约验证失败。'
    }

    $versionOutput = @(& $releaseCliExe version)
    if ($LASTEXITCODE -ne 0 -or $versionOutput.Count -ne 1) {
        throw 'SeerNote.Cli.exe version 未返回单行成功结果。'
    }
    $versionEnvelope = $versionOutput[0] | ConvertFrom-Json
    if (-not $versionEnvelope.ok -or $versionEnvelope.data.version -ne '1.9.0') {
        throw "SeerNote.Cli.exe 版本验证失败：$($versionEnvelope.data.version)"
    }

    Write-Host 'PUBLISH_STRUCTURE_OK'
}

if ($Task -eq 'Clean') {
    if (Test-Path -LiteralPath $artifactRoot) {
        $resolvedArtifact = [IO.Path]::GetFullPath($artifactRoot)
        $resolvedRoot = [IO.Path]::GetFullPath($projectRoot) + [IO.Path]::DirectorySeparatorChar
        if (-not $resolvedArtifact.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw "拒绝清理项目目录之外的路径：$resolvedArtifact"
        }
        Remove-Item -LiteralPath $resolvedArtifact -Recurse -Force
    }
    return
}

$app = Build-App
$cli = Build-Cli $app
switch ($Task) {
    'Build' { Write-Host "Built: $app, $cli" }
    'Test' { Build-And-Run-Tests $app $cli }
    'Release' { Publish-App $app $cli }
    'Verify' {
        Build-And-Run-Tests $app $cli
        Publish-App $app $cli
        Verify-PublishStructure
        Write-Host 'VERIFY_OK'
    }
}
