<#
.SYNOPSIS

.DESCRIPTION

.PARAMETER 
#>
function Update-ProjectSchemaFromDatabase
{
    param(
        [parameter(Mandatory=$false)]
        [alias("p")]
        $ProjectName = "",
        [parameter(Mandatory=$false)]
        [alias("c")]
        $ScmpFileName = "",
        [parameter(Mandatory=$false)]
        [alias("r")]
        $RetryCount = 10
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $ProjectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($ProjectName) not found using currently selected project"
		$project = Get-Project
	}

	if(-not([System.IO.Path]::GetExtension($project.FullName) -eq ".sqlproj")){
		Write-Error "The Project $($project.FullName) is not a valid project file"
		Exit-PSSession -1
		Exit
	}

    $projItems = $project.ProjectItems
    $projDir = [System.IO.Path]::GetDirectoryName($project.FullName)
	$solDir = [System.IO.Path]::GetDirectoryName($dte.Solution.FullName)
	$ScmpFileName = iex "`"$ScmpFileName`""

	if(([System.String]::IsNullOrEmpty($ScmpFileName)) -or (-not (Test-Path $ScmpFileName))){
		$scmpItem = $project.ProjectItems | ?{($_.Properties -ne $null) -and ([System.IO.Path]::GetExtension($_.Properties.Item("FullPath").Value) -eq ".scmp")} | Select-Object -First 1
		$ScmpFileName = $scmpItem.Properties.Item("FullPath").Value
	}

	if(-not (Test-Path $ScmpFileName)){
		Write-Error "Schema compare file $($ScmpFileName) not found"
		Exit-PSSession -1
		Exit
	}
	
	Write-Output "Opening schema compare file $($ScmpFileName)..."
	$dte.ExecuteCommand("File.OpenFile",$ScmpFileName)

	Write-Host "Comparing items..."
	$dte.ExecuteCommand("SQL.SSDTSchemaCompareCompare")

	Write-Host "Upating target..."
	$done = $false
	$tries = 1
	do{
		try
		{
			$dte.ExecuteCommand("SQL.SSDTSchemaCompareWriteUpdates")
			$done = $true
		}
		catch	
		{
			$tries = $tries + 1
			Start-Sleep -s 2
		}
	}
	until($done -or ($tries -gt $RetryCount))
}

<#
.SYNOPSIS

.DESCRIPTION

.PARAMETER 
#>
function Update-DatabaseFromDbContextLibrary
{
    param(
        [parameter(Mandatory=$false)]
        [alias("p")]
        $ProjectName = "",
        [parameter(Mandatory=$false)]
        [alias("c")]
        $ConfigFileName = "",
        [parameter(Mandatory=$false)]
        [alias("f")]
		[switch]$Force = $false
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $ProjectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($ProjectName) not found using currently selected project"
		$project = Get-Project
	}

    $projItems = $project.ProjectItems
    $projDir = [System.IO.Path]::GetDirectoryName($project.FullName)
	$solDir = [System.IO.Path]::GetDirectoryName($dte.Solution.FullName)
	$outputPath = $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
	$outputFileName = $project.Properties.Item("OutputFileName").Value
	$targetPath = "$($projDir)\$($outputPath)"
	$target = "$($targetPath)$($outputFileName)"
	$ConfigFileName = iex "`"$ConfigFileName`""

	if([System.String]::IsNullOrEmpty($ConfigFileName) -or (-not (Test-Path $ConfigFileName))){
		$ConfigFileName = "$($target).config"
	}

	$build = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToBuildToolsFile(“msbuild.exe”, [Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion,[Microsoft.Build.Utilities.DotNetFrameworkArchitecture]::Bitness64)
	. $build $dte.Solution.FullName /t:Build

	$relative = Join-Path -Path $PSScriptRoot -ChildPath ..\lib\net452
	$migrateExe = "$(Resolve-Path -Path $relative)\MigrateSqlDatabase.exe"
	$configOption = ""
	$forceOption = ""

	if(Test-Path $ConfigFileName){
		$configOption = " -c `"$($ConfigFileName)`""
	}
	
	if($Force){
		$forceOption = " -f"
	}

	$cmd = "$($migrateExe) -l `"$($target)`" $($configOption) $($forceOption)"
	iex $cmd
}

<#
.SYNOPSIS

.DESCRIPTION

.PARAMETER 
#>
function Update-DatabaseSchemaFromProject
{
    param(
        [parameter(Mandatory=$false)]
        [alias("p")]
        $ProjectName = "",
        [parameter(Mandatory=$true)]
        [alias("c")]
        $PublishConfig = "",
        [parameter(Mandatory=$false)]
        [alias("v")]
        $SqlCommandVarsFile = ""
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $ProjectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($ProjectName) not found using currently selected project"
		$project = Get-Project
	}

	if(-not([System.IO.Path]::GetExtension($project.FullName) -eq ".sqlproj")){
		Write-Error "The Project $($project.FullName) is not a valid project file"
		Exit-PSSession -1
		Exit
	}

    $projItems = $project.ProjectItems
    $projDir = [System.IO.Path]::GetDirectoryName($project.FullName)
	$solDir = [System.IO.Path]::GetDirectoryName($dte.Solution.FullName)
	$PublishConfig = iex "`"$PublishConfig`""
	$SqlCommandVarsFile = iex "`"$SqlCommandVarsFile`""
	$sqlCommandVarsOption = ""

	if([System.String]::IsNullOrEmpty($PublishConfig) -or (-not (Test-Path $PublishConfig))){
		Write-Error "The publish config file $($PublishConfig) is missing but is requied"
		Exit-PSSession -1
		Exit
	}

	$build = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToBuildToolsFile(“msbuild.exe”, [Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion,[Microsoft.Build.Utilities.DotNetFrameworkArchitecture]::Bitness64)
	. $build $dte.Solution.FullName /t:Build

	if([System.String]::IsNullOrEmpty($SqlCommandVarsFile) -or (-not (Test-Path $SqlCommandVarsFile))){
		$sqlCommandVarsOption = " /p:SqlCommandVarsFile=`"$($SqlCommandVarsFile)`""
	}
	
	$cmd = "$($build) `"$($project.FullName)`" /t:Deploy /p:DeploymentConfigurationFile=`"$($PublishConfig)`" /p:UseSandboxSettings=false $($sqlCommandVarsOption)"
	iex $cmd
}

Export-ModuleMember @( 'Update-ProjectSchemaFromDatabase', 'Update-DatabaseFromDbContextLibrary', 'Update-DatabaseSchemaFromProject' )
