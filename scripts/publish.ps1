param(
    [ValidateSet("linux-x64", "win-x64", "osx-arm64", "osx-x64", "all")]
    [string]$Rid = "win-x64",
    [switch]$WithLocalPatches
)

$ErrorActionPreference = "Stop"
Push-Location (Join-Path $PSScriptRoot "..")

try {
    $rids = if ($Rid -eq "all") {
        @("linux-x64", "win-x64", "osx-arm64", "osx-x64")
    } else {
        @($Rid)
    }

    foreach ($r in $rids) {
        $out = Join-Path $PWD "artifacts/$r"

        foreach ($p in @("Desktop", "Cli")) {
            dotnet publish "src/ShadowHeartsItalianPatcher.$p" -c Release -r $r --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false -o $out

            if ($LASTEXITCODE -ne 0) {
                throw "Pubblicazione fallita: $r / $p"
            }
        }

        New-Item -ItemType Directory -Force (Join-Path $out "Patches") | Out-Null
        Copy-Item README.md, PATCHING_ARCHITECTURE.md, THIRD_PARTY_NOTICES.md, LICENSE $out
        Copy-Item licenses, docs $out -Recurse -Force

        if ($WithLocalPatches) {
            Copy-Item Patches/* (Join-Path $out "Patches") -Recurse -Force
        } else {
            Copy-Item Patches/README.md (Join-Path $out "Patches")
        }
    }
} finally {
    Pop-Location
}
