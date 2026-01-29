# Публикация бота как single-file exe
dotnet publish -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

# Удаляем pdb
Remove-Item ".\bin\Release\net8.0-windows\win-x64\publish\*.pdb" -ErrorAction SilentlyContinue

Write-Host "`nГотово:" -ForegroundColor Green
Write-Host ".\bin\Release\net8.0-windows\win-x64\publish\" -ForegroundColor Cyan
Get-ChildItem ".\bin\Release\net8.0-windows\win-x64\publish\" | Format-Table Name, @{N="Size";E={"{0:N0} KB" -f ($_.Length/1KB)}}
