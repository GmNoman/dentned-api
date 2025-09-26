# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file
COPY ["DentneDAPI/DentneDAPI.csproj", "DentneDAPI/"]
RUN dotnet restore "DentneDAPI/DentneDAPI.csproj"

# Copy everything else
COPY . .
WORKDIR "/src/DentneDAPI"
RUN dotnet build "DentneDAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DentneDAPI.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
ENTRYPOINT ["dotnet", "DentneDAPI.dll"]