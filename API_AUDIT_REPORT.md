# API_AUDIT_REPORT.md

## Section 1: Critical Bugs

### 1.1 Logic Flaw in MRP Calculation
**File:** `RTROPToLogoIntegration/Application/Features/MRP/Commands/ProcessMrpCommand.cs`

**Issue:** The MRP logic fails to strictly adhere to the requirement "Orders must only be created if (Stock + OpenPO) < ROP".
The current implementation calculates `need` based on the difference between `ROP` and `NetStock` (`Stock + OpenPO`), but it does not check if `NetStock` is actually less than `ROP` before proceeding.

**Code:**
```csharp
var netStock = stockQty + openPo;
var ropGap = item.ROP - netStock;
double need = item.ROP_update_OrderQuantity - ropGap;

if (need > 0 && item.PlanningType == "MTS")
{
    // ... Order created ...
}
```

**Scenario:**
- `Stock` = 100
- `OpenPO` = 0
- `ROP` = 50
- `NetStock` = 100
- `ropGap` = 50 - 100 = -50
- `Incoming (OrderQuantity)` = 10
- `need` = 10 - (-50) = 60

Even though `Stock (100) > ROP (50)`, the logic calculates a need of 60 and proceeds to create an order. This violates the business rule to only order if `(Stock + OpenPO) < ROP`.

### 1.2 SQL Injection Vulnerability (Dynamic Table Names)
**File:** `RTROPToLogoIntegration/Infrastructure/Persistence/StockRepository.cs`

**Issue:** The application uses string interpolation to construct SQL queries with dynamic table names derived from user input (`firmNo`, `periodNr`). These inputs are not validated against a whitelist or sanitized, making the application vulnerable to SQL Injection attacks if malicious values are passed via the API (e.g., `process-mrp` command).

**Code:**
```csharp
string sql = $@"
    SELECT SUM(ORFL.AMOUNT - ORFL.SHIPPEDAMOUNT) AS TOTAL
    FROM LG_{firmNo}_{periodNr}_ORFLINE ORFL
    ...";
```
If `firmNo` is manipulated to be `123; DROP TABLE LG_123_ITEMS; --`, it could lead to data loss or unauthorized access.

## Section 2: Performance Risks

### 2.1 N+1 Query Problem (Looping Database Calls)
**File:** `RTROPToLogoIntegration/Application/Features/MRP/Commands/ProcessMrpCommand.cs`

**Issue:** The `ProcessMrpCommandHandler` iterates over the list of items (`request.Items`) and executes multiple database queries for *each item* inside the `foreach` loop. This is a classic N+1 performance anti-pattern.

**Code:**
```csharp
foreach (var item in request.Items)
{
    // ...
    var (itemRef, unitCode, _) = await _stockRepository.GetItemRefAndUnitByCodeAsync(...); // Query 1
    var stockQty = await _stockRepository.GetStockQuantityAsync(...); // Query 2
    var openPo = await _stockRepository.GetOpenPoQuantityAsync(...); // Query 3
    // ... potentially more queries ...
}
```
If the request contains 1000 items, the application will execute thousands of database queries sequentially, significantly degrading performance and potentially timing out.

### 2.2 HttpClient Socket Exhaustion
**File:** `RTROPToLogoIntegration/Infrastructure/Services/LogoClientService.cs`

**Issue:** The `LogoClientService` instantiates a new `HttpClient` in its constructor:
```csharp
_httpClient = new HttpClient();
```
The service is registered as `Scoped` in `Program.cs`. This means a new `HttpClient` instance (and underlying socket connection) is created for every HTTP request to the API. In high-load scenarios, this can lead to **Socket Exhaustion**, where the server runs out of available sockets to open new connections.

**Recommendation:** Use `IHttpClientFactory` to manage `HttpClient` instances efficiently.

## Section 3: Architecture & Security

### 3.1 Architecture (Wolverine Compliance)
**Status:** **PASSED** (with minor notes)

- The project correctly uses Wolverine's `IMessageBus` interface.
- No artifacts of MediatR (`IRequest`, `IRequestHandler`) were found in the codebase.
- The command handler `ProcessMrpCommandHandler` is a plain class (POCO) that follows Wolverine's convention-based discovery.
- `Program.cs` includes `builder.Host.UseWolverine(...)`.

### 3.2 Hardcoded Secrets
**File:** `RTROPToLogoIntegration/appsettings.json`

**Issue:** Sensitive information, including database passwords and API keys, is hardcoded in the configuration file.

- `ConnectionStrings:DefaultConnection` contains `Password=1`.
- `ConnectionStrings:LogoConnection` contains `Password=1`.
- `LogoRestSettings:Password` contains `"5346"`.
- `LogoRestSettings:ApiKey` contains a base64 string.
- `JwtSettings:SecretKey` is hardcoded.

**Recommendation:** Move these secrets to User Secrets (for development) or a secure vault/environment variables (for production).

### 3.3 Sensitive Data Logging
**File:** `RTROPToLogoIntegration/Infrastructure/Services/LogoClientService.cs`

**Issue:** The application logs sensitive data, including passwords and API keys, to the console in plain text.

**Code:**
```csharp
System.Console.WriteLine($"DEBUG: Password: {password}");
System.Console.WriteLine($"DEBUG: ApiKey (Raw): {apiKey}");
```
This poses a significant security risk as these logs can be accessed by unauthorized personnel or monitoring tools.
