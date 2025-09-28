# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the project file and restore dependencies. This layer is cached 
# unless the project file changes, speeding up subsequent builds.
COPY ["DentneDAPI/DentneDAPI.csproj", "DentneDAPI/"]
RUN dotnet restore "DentneDAPI/DentneDAPI.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/DentneDAPI"
RUN dotnet publish "DentneDAPI.csproj" -c Release -o /app/publish

# Final runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://*:8080
ENTRYPOINT ["dotnet", "DentneDAPI.dll"]