function IsUpgradeRequired($oldPackageVersion, $packageVersion)
{
    #handles 13.1.7340-preview or 13.1.7340-beta versions
    $oldPackageVersion = $oldPackageVersion.split('-')[0];
    $packageVersion = $packageVersion.split('-')[0];

	return [System.Version]$packageVersion -gt [System.Version]$oldPackageVersion
}

$basePath = $PSScriptRoot
$logFileName = $basePath + '\result.log'
$progressLogFile = $basePath + "\progress.log"

if (Test-Path $logFileName) 
{
	Remove-Item $logFileName
}
if (Test-Path $progressLogFile) 
{
	Remove-Item $progressLogFile
}

Try
{
	$xml = [xml](Get-Content ($basePath + '\config.xml'))

	$projectCounter = 1
	$projects = $xml.config.project
	foreach($project in $projects)
	{
		$projectName = $project.name
		
		"`nUpdating project '$projectName'"
		
		$packages = $project.package
		$packageCounter = 1
		$totalCount = @($packages).Count
		foreach ($package in $packages)
		{
			$packageName = $package.name
			$packageVersion = $package.version
			
			"`npackage '$packageName' version '$packageVersion'"
			
			$projectPackages = Get-Package -ProjectName $projectName
			$oldPackage = $projectPackages | Where-Object { $_.Id -eq $packageName }
			$oldPackageVersion = if(!$oldPackage.Version) { $null } else { $oldPackage.Version.ToString() }
			$isUpdateRequired = IsUpgradeRequired $oldPackageVersion $packageVersion
			
			if($isUpdateRequired)
			{
				if($oldPackageVersion -ne $null -and $oldPackageVersion -ne $packageVersion -and $oldPackageVersion -ne ($packageVersion + '.0') -and ($oldPackageVersion + '.0') -ne $packageVersion)
				{
					"`nupgrading from '$oldPackageVersion' to '$packageVersion'"
					Invoke-Expression "Update-Package -Id $packageName -ProjectName $projectName -Version $packageVersion -FileConflictAction OverwriteAll"
				}
				else
				{
					"`npackage already on version '$packageVersion'"
				}
			}
			else
			{
				"`npackage is on higher version '$oldPackageVersion' and will not be downgraded to '$packageVersion'"
			}
						
			$progressOut = "(" + $projectCounter + " \ " + @($projects).Count + ") --- " + $projectName + " --- " + $packageCounter.ToString() + ' / ' + $totalCount.ToString()
			$progressOut | Out-File -FilePath $progressLogFile
			$packageCounter = $packageCounter + 1
		}
		
		$projectCounter = $projectCounter + 1
	}
	
	New-Item -Path $basePath -Name "result.log" -ItemType "file" -Value "success"
}
Catch
{
	$text = "fail - " + $_.Exception.Message
	New-Item -Path $basePath -Name "result.log" -ItemType "file" -Value $text
}
Finally
{
	if (Test-Path $progressLogFile) 
	{
		Remove-Item $progressLogFile
	}
}