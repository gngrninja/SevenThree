# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY src/SevenThree/SevenThree.csproj src/SevenThree/
RUN dotnet restore src/SevenThree/SevenThree.csproj

# Copy everything else and build
COPY . .
RUN dotnet publish src/SevenThree/SevenThree.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Copy import folder if it exists (for question pools)
COPY --from=build /src/src/SevenThree/import ./import

# Create logs directory
RUN mkdir -p /app/logs

ENTRYPOINT ["dotnet", "SevenThree.dll"]
