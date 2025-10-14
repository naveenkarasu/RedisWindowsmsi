@{
    # Script module or binary module file associated with this manifest
    RootModule = 'RedisInstaller.psm1'
    
    # Version number of this module
    ModuleVersion = '1.0.0'
    
    # ID used to uniquely identify this module
    GUID = 'a1b2c3d4-e5f6-4a5b-8c9d-0e1f2a3b4c5d'
    
    # Author of this module
    Author = 'Redis Windows MSI Project'
    
    # Company or vendor of this module
    CompanyName = 'Redis Community'
    
    # Copyright statement for this module
    Copyright = '(c) 2025 Redis Windows MSI Project Contributors. MIT License.'
    
    # Description of the functionality provided by this module
    Description = 'PowerShell module for detecting, installing, and configuring Redis backends (WSL2/Docker) on Windows systems.'
    
    # Minimum version of the PowerShell engine required by this module
    PowerShellVersion = '5.1'
    
    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()
    
    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @()
    
    # Script files (.ps1) that are run in the caller's environment prior to importing this module
    ScriptsToProcess = @()
    
    # Type files (.ps1xml) to be loaded when importing this module
    TypesToProcess = @()
    
    # Format files (.ps1xml) to be loaded when importing this module
    FormatsToProcess = @()
    
    # Functions to export from this module
    FunctionsToExport = @(
        'Get-WindowsVersion',
        'Test-IsAdministrator',
        'Write-LogMessage',
        'Test-CommandExists',
        'Download-File',
        # Detection functions (to be added)
        'Test-WSL2Installed',
        'Test-DockerInstalled',
        'Get-RedisBackend',
        # Installation functions (to be added)
        'Install-WSL2',
        'Install-DockerDesktop',
        'Install-RedisInWSL'
    )
    
    # Cmdlets to export from this module
    CmdletsToExport = @()
    
    # Variables to export from this module
    VariablesToExport = @()
    
    # Aliases to export from this module
    AliasesToExport = @()
    
    # List of all modules packaged with this module
    ModuleList = @()
    
    # List of all files packaged with this module
    FileList = @('RedisInstaller.psm1', 'RedisInstaller.psd1')
    
    # Private data to pass to the module specified in RootModule/ModuleToProcess
    PrivateData = @{
        PSData = @{
            # Tags applied to this module
            Tags = @('Redis', 'WSL2', 'Docker', 'Windows', 'Installer')
            
            # A URL to the license for this module
            LicenseUri = 'https://github.com/naveenkarasu/RedisWindowsmsi/blob/main/LICENSE'
            
            # A URL to the main website for this project
            ProjectUri = 'https://github.com/naveenkarasu/RedisWindowsmsi'
            
            # ReleaseNotes of this module
            ReleaseNotes = 'Initial release with WSL2 and Docker detection and installation capabilities.'
        }
    }
    
    # HelpInfo URI of this module
    HelpInfoURI = 'https://github.com/naveenkarasu/RedisWindowsmsi/wiki'
}

