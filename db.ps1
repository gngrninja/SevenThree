#! /usr/local/bin/pwsh

dotnet ef migrations remove
dotnet ef migrations add initial
dotnet ef database update
