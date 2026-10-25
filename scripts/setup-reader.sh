#!/usr/bin/env bash
set -euo pipefail

# sqlcmd substitutes this environment variable into a SQL string literal.
# Restrict the developer password alphabet before substitution; never echo it.
if [[ ${#CATALOG_HTTP_PASSWORD} -lt 16 || "$CATALOG_HTTP_PASSWORD" == *[^a-zA-Z0-9_@!#%+.-]* ]]; then
  printf '%s\n' 'CATALOG_HTTP_PASSWORD must have at least 16 characters using letters, digits or _@!#%+.-' >&2
  exit 1
fi

/opt/mssql-tools18/bin/sqlcmd -S sqlserver -U sa -C -b <<'SQL'
IF SUSER_ID(N'catalog_http') IS NULL
    CREATE LOGIN [catalog_http] WITH PASSWORD = N'$(CATALOG_HTTP_PASSWORD)', CHECK_POLICY = ON;
GO
USE [ProductCatalog];
GO
IF USER_ID(N'catalog_http') IS NULL
    CREATE USER [catalog_http] FOR LOGIN [catalog_http];
GRANT SELECT ON OBJECT::dbo.Products TO [catalog_http];
GO
SQL
