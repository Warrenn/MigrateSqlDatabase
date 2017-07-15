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
        $projectName = "",
        [parameter(Mandatory=$false)]
        [alias("c")]
        $scmpFileName = ""
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $projectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($projectName) not found using currently selected project"
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
	$scmpFileName = iex "`"$scmpFileName`""

	if(([System.String]::IsNullOrEmpty($scmpFileName)) -or (-not (Test-Path $scmpFileName))){
		$scmpItem = $project.ProjectItems | ?{($_.Properties -ne $null) -and ([System.IO.Path]::GetExtension($_.Properties.Item("FullPath").Value) -eq ".scmp")} | Select-Object -First 1
		$scmpFileName = $scmpItem.Properties.Item("FullPath").Value
	}

	if(-not (Test-Path $scmpFileName)){
		Write-Error "Schema compare file $($scmpFileName) not found"
		Exit-PSSession -1
		Exit
	}
	
	Write-Output "Opening schema compare file $($scmpFileName)..."
	$dte.ExecuteCommand("File.OpenFile",$scmpFileName)

	Write-Host "Comparing items..."
	$dte.ExecuteCommand("SQL.SSDTSchemaCompareCompare")

	Write-Host "Upating target..."
	$done = $false
	$tries = 1
	do{
		try
		{
			Write-Host $dte.Mode

			$dte.ExecuteCommand("SQL.SSDTSchemaCompareWriteUpdates")
			$done = $true
		}
		catch	
		{
			$error[0]
			$tries = $tries + 1
			Start-Sleep -s 2
		}
	}
	until($done -or ($tries -gt 10))
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
        $projectName = "",
        [parameter(Mandatory=$false)]
        [alias("c")]
        $configFileName = ""
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $projectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($projectName) not found using currently selected project"
		$project = Get-Project
	}

    $projItems = $project.ProjectItems
    $projDir = [System.IO.Path]::GetDirectoryName($project.FullName)
	$solDir = [System.IO.Path]::GetDirectoryName($dte.Solution.FullName)
	$outputPath = $project.ConfigurationManager.ActiveConfiguration.Properties.Item("OutputPath").Value
	$outputFileName = $project.Properties.Item("OutputFileName").Value
	$targetPath = "$($projDir)\$($outputPath)"
	$target = "$($targetPath)$($outputFileName)"
	$configFileName = iex "`"$configFileName`""

	if([System.String]::IsNullOrEmpty($configFileName) -or (-not (Test-Path $configFileName))){
		$configFileName = "$($target).config"
	}

	$build = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToBuildToolsFile(“msbuild.exe”, [Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion,[Microsoft.Build.Utilities.DotNetFrameworkArchitecture]::Bitness64)
	. $build $dte.Solution.FullName /t:Build

	$relative = Join-Path -Path $PSScriptRoot -ChildPath ..\lib\net452
	$migrateExe = "$(Resolve-Path -Path $relative)\MigrateSqlDatabase.exe"

	if(Test-Path $configFileName){
		. $migrateExe -l "$($target)" -c "$($configFileName)"
	}
	else{
		. $migrateExe -l "$($target)" 
	}
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
        $projectName = "",
        [parameter(Mandatory=$true)]
        [alias("c")]
        $publishConfig = ""
    )
	
    $project = ($dte.Solution.Projects | ?{$_.Name -eq $projectName}) | Select-Object -First 1

	if($project -eq $null){
		Write-Warning "Project $($projectName) not found using currently selected project"
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
	$publishConfig = iex "`"$publishConfig`""

	if([System.String]::IsNullOrEmpty($publishConfig) -or (-not (Test-Path $publishConfig))){
		Write-Error "The publish config file $($publishConfig) is missing but is requied"
		Exit-PSSession -1
		Exit
	}

	$build = [Microsoft.Build.Utilities.ToolLocationHelper]::GetPathToBuildToolsFile(“msbuild.exe”, [Microsoft.Build.Utilities.ToolLocationHelper]::CurrentToolsVersion,[Microsoft.Build.Utilities.DotNetFrameworkArchitecture]::Bitness64)
	. $build $dte.Solution.FullName /t:Build
	. $build "$($project.FullName)" /t:Deploy /p:DeploymentConfigurationFile="$($publishConfig)" /p:UseSandboxSettings=false 
}

Export-ModuleMember @( 'Update-ProjectSchemaFromDatabase', 'Update-DatabaseFromDbContextLibrary', 'Update-DatabaseSchemaFromProject' )
