param(
    [Parameter(Mandatory=$true)][string]$ManagedPath,
    [Parameter(Mandatory=$true)][string]$CecilPath,
    [string]$ModPath = (Join-Path $PSScriptRoot '..\local\CopyIt\CopyIt.dll')
)
$ErrorActionPreference='Stop'
Add-Type -Path $CecilPath
$resolver=[Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($ManagedPath)
$resolver.AddSearchDirectory([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($ModPath)))
$parameters=[Mono.Cecil.ReaderParameters]::new()
$parameters.AssemblyResolver=$resolver
$game=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ManagedPath 'Game.dll'),$parameters)
$mod=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($ModPath),$parameters)
try {
    $modType=$mod.MainModule.GetType('CopyIt.Mod')
    $expected=($modType.Fields | Where-Object Name -eq 'TestedGameHash').Constant
    if((Get-FileHash (Join-Path $ManagedPath 'Game.dll')).Hash -ne $expected){throw 'Game hash is not the reviewed build'}
    $native=$game.MainModule.GetType('Game.Tools.ObjectToolBaseSystem')
    $create=@($native.Methods | Where-Object Name -eq 'CreateDefinitions')
    if($create.Count -ne 1 -or $create[0].Parameters.Count -ne 24){throw 'Unexpected placement API'}
    if($create[0].Parameters[22].ParameterType.FullName -ne 'Game.Tools.PlacementOverrides' -or
       $create[0].Parameters[23].ParameterType.FullName -ne 'Unity.Jobs.JobHandle'){throw 'Unexpected placement arguments'}
    $job=$game.MainModule.GetType('Game.Tools.BulldozeToolSystem').NestedTypes | Where-Object Name -eq 'CreateDefinitionsJob'
    if(!$job -or !($job.Methods | Where-Object {$_.Name -eq 'Execute' -and $_.Parameters.Count -eq 0})){throw 'Bulldozer job unavailable'}
    $fixed=@{
        m_EditorMode='System.Boolean'; m_Mode='Game.Tools.BulldozeToolSystem/Mode';
        m_State='Game.Tools.BulldozeToolSystem/State';
        m_ControlPoints='Unity.Collections.NativeList`1<Game.Tools.ControlPoint>';
        m_CommandBuffer='Unity.Entities.EntityCommandBuffer'
    }
    $fields=@($job.Fields | Where-Object IsPublic)
    foreach($field in $fields){
        $name=$field.FieldType.FullName
        if($fixed.ContainsKey($field.Name)){
            if($fixed[$field.Name] -ne $name){throw "Changed bulldozer field: $($field.Name) $name"}
        } elseif($name -notmatch '^Unity.Entities.(ComponentLookup|BufferLookup)`1<'){
            throw "Unhandled bulldozer field: $($field.Name) $name"
        }
    }
    foreach($name in $fixed.Keys){if($name -notin $fields.Name){throw "Missing bulldozer field: $name"}}
    $resolved=0
    foreach($member in $mod.MainModule.GetMemberReferences()){
        if($member.DeclaringType.Scope.Name -ne 'Game') {continue}
        if(!$member.Resolve()){throw "Unresolved game member: $member"}
        $resolved++
    }
    "PASS: reviewed Game.dll hash, 24-argument placement API, $($fields.Count) native bulldozer fields, $resolved game member references. Metadata checks only; not in-game execution."
} finally {$mod.Dispose();$game.Dispose();$resolver.Dispose()}
