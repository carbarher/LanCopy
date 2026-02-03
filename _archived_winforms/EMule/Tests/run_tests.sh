#!/bin/bash
# Script para compilar y ejecutar tests de integración eMule
# Requiere: .NET SDK instalado y amuled corriendo

echo "========================================"
echo "Tests de Integración eMule - SlskDown"
echo "========================================"
echo ""

# Verificar que amuled está corriendo
echo "Verificando si amuled está corriendo..."
if pgrep -x "amuled" > /dev/null; then
    echo "[OK] amuled está corriendo"
else
    echo "[ADVERTENCIA] amuled no está corriendo"
    echo "Por favor inicia amuled antes de ejecutar los tests:"
    echo "  amuled -f"
    echo ""
    read -p "Presiona Enter para continuar de todos modos..."
fi

echo ""
echo "Compilando proyecto de tests..."
cd "$(dirname "$0")"

# Crear archivo de proyecto temporal si no existe
if [ ! -f "EMuleClientTests.csproj" ]; then
    echo "Creando archivo de proyecto..."
    cat > EMuleClientTests.csproj << 'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <RootNamespace>SlskDown.EMule.Tests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../SlskDown.csproj" />
  </ItemGroup>
</Project>
EOF
fi

# Compilar
dotnet build EMuleClientTests.csproj -c Release
if [ $? -ne 0 ]; then
    echo ""
    echo "[ERROR] Falló la compilación"
    read -p "Presiona Enter para salir..."
    exit 1
fi

echo ""
echo "========================================"
echo "Ejecutando tests..."
echo "========================================"
echo ""

# Ejecutar tests
dotnet run --project EMuleClientTests.csproj -c Release

echo ""
echo "========================================"
echo "Tests finalizados"
echo "========================================"
read -p "Presiona Enter para salir..."
