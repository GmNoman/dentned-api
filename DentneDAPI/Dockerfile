# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file
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
EXPOSE 80
ENV ASPNETCORE_URLS=http://*:80
ENTRYPOINT ["dotnet", "DentneDAPI.dll"]