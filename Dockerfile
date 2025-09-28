FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore as distinct layers
COPY ["DentneDAPI/DentneDAPI.csproj", "DentneDAPI/"]
RUN dotnet restore "DentneDAPI/DentneDAPI.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/DentneDAPI"
RUN dotnet build "DentneDAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DentneDAPI.csproj" -c Release -o /app/publish

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://*:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DentneDAPI.dll"]