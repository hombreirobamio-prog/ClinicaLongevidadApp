param(
    [switch] $AllTests
)

Write-Host "Restaurando paquetes..."
if (-not (dotnet restore)) {
    Write-Error "dotnet restore falló."
    exit 1
}

Write-Host "Compilando solución..."
if (-not (dotnet build -c Debug)) {
    Write-Error "dotnet build falló."
    exit 2
}

$testProject = ".\ClinicaLongevidadApp.Tests\ClinicaLongevidadApp.Tests.csproj"

if ($AllTests) {
    Write-Host "Ejecutando todas las pruebas del proyecto de tests..."
    dotnet test $testProject --logger "console;verbosity=detailed"
    exit $LASTEXITCODE
}
else {
    Write-Host "Ejecutando sólo las pruebas de AuditoriaDetallesHelperTests..."
    dotnet test $testProject --filter "FullyQualifiedName~AuditoriaDetallesHelperTests" --logger "console;verbosity=detailed"
    exit $LASTEXITCODE
}