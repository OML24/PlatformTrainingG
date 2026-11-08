# ZU LMS

Training management system for Zarqa University, built with ASP.NET Core 10 and PostgreSQL.

## Project layout

- `src/`: application, domain, infrastructure, and web portal
- `tests/`: automated tests
- `deploy/`: Docker and nginx deployment files
- `docs/`: architecture, operations, and QA documentation
- `template/` and `prototype/`: UI assets and prototype

## Build and test

Install the .NET SDK version in `global.json`, then run:

```sh
dotnet restore ZuLms.sln
dotnet build ZuLms.sln
dotnet test ZuLms.sln
```

PostgreSQL integration tests use a PostgreSQL 17 test container. For deployment, copy `deploy/.env.sample` to `deploy/.env` and supply real values locally; the filled `.env` file is ignored by Git.

## Recent correction

The quality cancellation page at `/quality/cancellations` lists pending cancellation requests submitted by deans for approved courses, regardless of whether the course is managed by quality. Quality staff can approve or reject those requests.
