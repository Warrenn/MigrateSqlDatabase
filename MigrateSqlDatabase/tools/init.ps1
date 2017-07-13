param($installPath, $toolsPath, $package, $project)

if (Get-Module | ?{ $_.Name -eq 'MigrateSqlDatabase' })
{
    Remove-Module MigrateSqlDatabase
}

Import-Module (Join-Path $toolsPath MigrateSqlDatabase.psd1)
