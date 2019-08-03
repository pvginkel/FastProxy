function LoadResults($Path)
{
    $Headers = $Null
    $Data = @( )

    foreach ($Line in Get-Content $Path)
    {
        $Values = $Line.Split(',');

        if ($Headers -eq $Null)
        {
            $Headers = $Values;
        }
        else
        {
            $Object = @{ }
            $Data += $Object

            for ($i = 0; $i -lt $Headers.Length; $i++)
            {
                $Object[$Headers[$i]] = $Values[$i]
            }
        }
    }
    
    return $Data
}

function ParseTime($Time)
{
    if ($Time.EndsWith(" us"))
    {
        return $Time.Substring(0, $Time.Length - 3);
    }

    return $Time;
}

function Get-Baseline($Data)
{
    $Baseline = @{ }

    foreach ($Object in $Data | Where-Object { $_["Toolchain"] -eq "Default" -and $_["ContinueMode"] -eq "Direct" })
    {
        $Baseline[$Object["BlockSize"]] = ParseTime($Object["Mean"])
    }

    return $Baseline;
}

function Get-Distinct($Data, $Name, $Initial = $Null)
{
    $Values = @( )

    if ($Initial -ne $Null)
    {
        $Values += $Initial
    }

    foreach ($Object in $Data)
    {
        if (-not ($Values -contains $Object[$Name]))
        {
            $Values += $Object[$Name]
        }
    }

    return $Values;
}

function Write-Markdown($Headers, $Results, $AlignRight)
{
    $Widths = @( )

    for ($i = 0; $i -lt $Headers.Length; $i++)
    {
        $Width = $Headers[$i].Length

        foreach ($Line in $Results)
        {
            $CellWidth = $Line[$i].Length
            if ($CellWidth -gt $Width)
            {
                $Width = $CellWidth
            }
        }

        $Widths += $Width
    }

    Write-MarkdownLine $Headers $Widths $AlignRight

    $Write = "|"

    for ($i = 0; $i -lt $Headers.Length; $i++)
    {
        $Cell = "";

        if ($AlignRight[$i])
        {
            $Cell = ":";
        }

        $Write += $Cell.PadLeft($Widths[$i] + 2, "-") + "|"
    }

    Write-Host $Write

    foreach ($Line in $Results)
    {
        Write-MarkdownLine $Line $Widths $AlignRight
    }
}

function Write-MarkdownLine($Line, $Widths, $AlignRight)
{
    $Write = "|"

    for ($i = 0; $i -lt $Line.Length; $i++)
    {
        if ($AlignRight[$i])
        {
            $Write += " " + $Line[$i].PadLeft($Widths[$i]) + " |"
        }
        else
        {
            $Write += " " + $Line[$i].PadRight($Widths[$i]) + " |"
        }
    }

    Write-Host $Write
}

$Path = (Split-Path -Path $MyInvocation.MyCommand.Definition -Parent) + "\bin\Release\netcoreapp3.0\BenchmarkDotNet.Artifacts\results"
$Report = "FastProxy.BenchmarkApp.EchoBenchmark-report.csv"

$Data = LoadResults "$Path\$Report"

$Baseline = Get-Baseline $Data
$Toolchains = Get-Distinct $Data "Toolchain" @( "Default" )
$ContinueModes = Get-Distinct $Data "ContinueMode"
$BlockSizes = Get-Distinct $Data "BlockSize"

$Headers = @( "Toolchain", "Block size" )

foreach ($ContinueMode in $ContinueModes)
{
    $Headers += $ContinueMode + " μs"
    $Headers += $ContinueMode + " %"
}

$Results = @( )

foreach ($Toolchain in $Toolchains)
{
    foreach ($BlockSize in $BlockSizes)
    {
        $Line = @( $Toolchain, $BlockSize )

        foreach ($ContinueMode in $ContinueModes)
        {
            $Object = $Data | Where-Object { $_["Toolchain"] -eq $Toolchain -and $_["BlockSize"] -eq $BlockSize -and $_["ContinueMode"] -eq $ContinueMode } | Select-Object
            $Mean = (ParseTime $Object["Mean"]) -as [double]
            $ThisBaseline = $Baseline[$BlockSize] -as [double]
            $Difference = (($Mean / $ThisBaseline) * 100 - 100) -as [int]

            $Line += (ParseTime $Object["Mean"]) + " μs"
            $Line += $Difference.ToString("0.0") + "%"
        }

        $Results += , $Line
    }
}

Write-Markdown $Headers $Results @( $False, $False, $True, $True, $True, $True, $True, $True )
