# ---------- Build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# pehle sirf csproj copy karke restore (Docker cache ka fayda)
COPY *.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Run ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
# Render PORT environment variable deta hai (Program.cs use padhta hai). Local docker ke liye default:
EXPOSE 8080

ENTRYPOINT ["dotnet", "PerfumeShop.Api.dll"]
