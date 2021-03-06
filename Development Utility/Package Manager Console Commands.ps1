﻿param( $installPath, $toolsPath, $package )

New-Module -ScriptBlock {
$installPath = $args[0]

function Update-DependentLogic {
	[CmdletBinding()]
	Param()
	Process {
		& "$installPath\Development Utility\EnterpriseWebLibrary.DevelopmentUtility" $installPath\..\.. UpdateAllDependentLogic
	}
}

function ExportLogic {
	[CmdletBinding()]
	Param()
	Process {
		& "$installPath\Development Utility\EnterpriseWebLibrary.DevelopmentUtility" $installPath\..\.. ExportLogic
	}
}

function Measure-LogicSize {
	[CmdletBinding()]
	Param()
	Process {
		& "$installPath\Development Utility\EnterpriseWebLibrary.DevelopmentUtility" $installPath\..\.. GetLogicSize
	}
}

function InstallAndStartServices {
	[CmdletBinding()]
	Param()
	Process {
		& "$installPath\Development Utility\EnterpriseWebLibrary.DevelopmentUtility" $installPath\..\.. InstallAndStartServices
	}
}

function StopAndUninstallServices {
	[CmdletBinding()]
	Param()
	Process {
		& "$installPath\Development Utility\EnterpriseWebLibrary.DevelopmentUtility" $installPath\..\.. StopAndUninstallServices
	}
}

} -ArgumentList $installPath