# Multi-stage Docker build for ZU-LMS (11 §1).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props .
COPY src/ZuLms.Domain/*.csproj src/ZuLms.Domain/
COPY src/ZuLms.Application/*.csproj src/ZuLms.Application/
COPY src/ZuLms.Infrastructure/*.csproj src/ZuLms.Infrastructure/
COPY src/ZuLms.Web/*.csproj src/ZuLms.Web/
RUN dotnet restore src/ZuLms.Web/ZuLms.Web.csproj
COPY . .
RUN dotnet publish src/ZuLms.Web -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
# fontconfig + FreeType are required by QuestPDF for certificate rendering.
RUN apt-get update \
    && apt-get install -y --no-install-recommends fontconfig libfreetype6 curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
# Run as a non-root user.
RUN useradd --uid 10001 --create-home zulms \
    && mkdir -p /var/zu-lms/storage /var/zu-lms/logs /home/zulms/.aspnet/DataProtection-Keys \
    && chown -R zulms:zulms /app /var/zu-lms /home/zulms/.aspnet
USER zulms
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD [ "/bin/sh", "-c", "curl -fsS http://localhost:8080/health || exit 1" ]
ENTRYPOINT ["dotnet", "ZuLms.Web.dll"]
