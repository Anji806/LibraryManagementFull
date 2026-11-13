
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["LibraryManagement/LibraryManagement.csproj", "LibraryManagement/"]
RUN dotnet restore "LibraryManagement/LibraryManagement.csproj"

COPY . .
WORKDIR /src/LibraryManagement
RUN dotnet publish "LibraryManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

# Install the missing Linux GSSAPI/Kerberos library
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 10000

CMD ["sh", "-c", "dotnet LibraryManagement.dll --urls http://0.0.0.0:${PORT:-10000}"]
