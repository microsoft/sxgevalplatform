# Client Application Onboarding Guide

**Date**: January 2025  
**Purpose**: Onboard client applications to access SXG Evaluation Platform API  
**Prerequisite**: App Registration completed (Manual or via script)

---

## ?? **Overview**

This guide covers onboarding client applications for **two authentication flows**:

1. **App-to-App Flow** (Managed Identity / Service Principal)
   - Azure Functions, Logic Apps, other Azure services
   - Service-to-service authentication
   - No user interaction

2. **Delegated User Flow** (OAuth 2.0)
   - Web apps, desktop apps, mobile apps
   - User signs in and grants consent
   - Acts on behalf of the user

---

## ?? **Prerequisites**

Before onboarding clients, ensure you have:

? **API App Registration Details**:
- Application (Client) ID: `YOUR_API_CLIENT_ID`
- App ID URI: `api://YOUR_API_CLIENT_ID`
- Tenant ID: `YOUR_TENANT_ID`

? **Configured Resources** (from manual setup):
- App Role: `EvalPlatform.FullAccess` (for app-to-app)
- OAuth Scope: `Evaluations.ReadWrite` (for delegated)

---

## ?? **Flow 1: App-to-App Authentication (Managed Identity)**

### **Use Case**
Azure services (Functions, Logic Apps, Container Apps) calling the API automatically without user interaction.

---

### **Step 1: Enable Managed Identity on Client Service**

#### **For Azure Function App**:

```bash
# Enable system-assigned managed identity
az functionapp identity assign \
  --name YOUR_FUNCTION_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP

# Get the managed identity's Object ID (Principal ID)
MANAGED_IDENTITY_ID=$(az functionapp identity show \
  --name YOUR_FUNCTION_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP \
  --query principalId -o tsv)

echo "Managed Identity Object ID: $MANAGED_IDENTITY_ID"
```

#### **For Azure Container App**:

```bash
# Enable system-assigned managed identity
az containerapp identity assign \
  --name YOUR_CONTAINER_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP

# Get the managed identity's Object ID
MANAGED_IDENTITY_ID=$(az containerapp identity show \
  --name YOUR_CONTAINER_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP \
  --query principalId -o tsv)

echo "Managed Identity Object ID: $MANAGED_IDENTITY_ID"
```

#### **For Azure Logic App**:

```bash
# Enable system-assigned managed identity
az logicapp identity assign \
  --name YOUR_LOGIC_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP

# Get the managed identity's Object ID
MANAGED_IDENTITY_ID=$(az logicapp identity show \
  --name YOUR_LOGIC_APP_NAME \
  --resource-group YOUR_RESOURCE_GROUP \
  --query principalId -o tsv)

echo "Managed Identity Object ID: $MANAGED_IDENTITY_ID"
```

---

### **Step 2: Assign App Role to Managed Identity**

Now assign the `EvalPlatform.FullAccess` app role to the managed identity.

#### **Option A: Azure Portal** (Easiest)

1. Go to **Azure Portal** ? **Azure Active Directory** ? **Enterprise applications**
2. Search for: `SXG-EvalPlatform-API-Dev` (your API app name)
3. Click on it
4. Go to **Users and groups** (left menu)
5. Click **Add user/group**
6. Click **Users and groups** ? **None Selected**
7. Search for your managed identity by name (e.g., `your-function-app-name`)
   - **Note**: Managed identities appear as service principals in this list
8. Select the managed identity
9. Click **Select**
10. Under **Select a role**, choose: `EvalPlatform.FullAccess`
11. Click **Assign**

#### **Option B: PowerShell** (Scripted)

```powershell
# Install Microsoft Graph module if needed
Install-Module Microsoft.Graph -Scope CurrentUser

# Connect to Microsoft Graph
Connect-MgGraph -Scopes "Application.ReadWrite.All", "AppRoleAssignment.ReadWrite.All"

# Get your API app registration
$apiApp = Get-MgServicePrincipal -Filter "displayName eq 'SXG-EvalPlatform-API-Dev'"
$apiAppId = $apiApp.Id

# Get the app role ID for EvalPlatform.FullAccess
$appRole = $apiApp.AppRoles | Where-Object { $_.Value -eq "EvalPlatform.FullAccess" }
$appRoleId = $appRole.Id

# Get the managed identity (service principal)
$managedIdentity = Get-MgServicePrincipal -Filter "displayName eq 'YOUR_MANAGED_IDENTITY_NAME'"
$managedIdentityId = $managedIdentity.Id

# Assign the app role to the managed identity
New-MgServicePrincipalAppRoleAssignment `
    -ServicePrincipalId $managedIdentityId `
    -PrincipalId $managedIdentityId `
    -ResourceId $apiAppId `
    -AppRoleId $appRoleId

Write-Host "? App role assigned successfully!"
```

#### **Option C: Azure CLI** (Alternative)

```bash
# Get API app's service principal ID
API_SP_ID=$(az ad sp list \
  --display-name "SXG-EvalPlatform-API-Dev" \
  --query "[0].id" -o tsv)

# Get the app role ID
APP_ROLE_ID=$(az ad sp show --id $API_SP_ID \
  --query "appRoles[?value=='EvalPlatform.FullAccess'].id" -o tsv)

# Get managed identity's service principal ID
MANAGED_IDENTITY_SP_ID=$(az ad sp list \
  --display-name "YOUR_MANAGED_IDENTITY_NAME" \
  --query "[0].id" -o tsv)

# Assign the app role
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/servicePrincipals/$MANAGED_IDENTITY_SP_ID/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{
    \"principalId\": \"$MANAGED_IDENTITY_SP_ID\",
    \"resourceId\": \"$API_SP_ID\",
  \"appRoleId\": \"$APP_ROLE_ID\"
  }"

echo "? App role assigned successfully!"
```

---

### **Step 3: Configure Client Application Code**

#### **For Azure Function (C#)**:

```csharp
using Azure.Identity;
using System.Net.Http;
using System.Net.Http.Headers;

public class EvalPlatformClient
{
    private readonly HttpClient _httpClient;
    private readonly DefaultAzureCredential _credential;
    private readonly string _apiBaseUrl;
    private readonly string _apiScope;

    public EvalPlatformClient(string apiBaseUrl, string apiClientId)
    {
        _httpClient = new HttpClient();
        _credential = new DefaultAzureCredential(); // Uses managed identity
        _apiBaseUrl = apiBaseUrl;
        _apiScope = $"api://{apiClientId}/.default"; // App-to-app scope
    }

    public async Task<string> GetAccessTokenAsync()
    {
      var tokenRequestContext = new Azure.Core.TokenRequestContext(
         new[] { _apiScope }
        );
        
        var token = await _credential.GetTokenAsync(tokenRequestContext);
    return token.Token;
    }

    public async Task<HttpResponseMessage> CallApiAsync(string endpoint)
    {
  // Get access token using managed identity
        var token = await GetAccessTokenAsync();
        
 // Set authorization header
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
      
        // Call the API
        var response = await _httpClient.GetAsync($"{_apiBaseUrl}/{endpoint}");
return response;
    }
}

// Usage in Azure Function
public class MyFunction
{
  private readonly EvalPlatformClient _client;

    public MyFunction()
    {
  _client = new EvalPlatformClient(
     apiBaseUrl: "https://sxg-eval-api-dev.azurewebsites.net",
            apiClientId: "YOUR_API_CLIENT_ID" // From App Registration
        );
    }

    [FunctionName("CallEvalPlatformAPI")]
    public async Task<IActionResult> Run(
      [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        var response = await _client.CallApiAsync("api/v1/eval/runs");
        
        if (response.IsSuccessStatusCode)
        {
     var content = await response.Content.ReadAsStringAsync();
      return new OkObjectResult(content);
        }
        
        return new StatusCodeResult((int)response.StatusCode);
    }
}
```

#### **For Azure Function (Python)**:

```python
import os
from azure.identity import DefaultAzureCredential
import requests

class EvalPlatformClient:
    def __init__(self, api_base_url, api_client_id):
      self.api_base_url = api_base_url
      self.api_scope = f"api://{api_client_id}/.default"
     self.credential = DefaultAzureCredential()  # Uses managed identity
    
 def get_access_token(self):
        """Get access token using managed identity"""
        token = self.credential.get_token(self.api_scope)
        return token.token
    
    def call_api(self, endpoint):
        """Call the API with managed identity authentication"""
        token = self.get_access_token()
        
headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json"
        }
     
        url = f"{self.api_base_url}/{endpoint}"
      response = requests.get(url, headers=headers)
        return response

# Usage in Azure Function
import azure.functions as func

def main(req: func.HttpRequest) -> func.HttpResponse:
    client = EvalPlatformClient(
        api_base_url="https://sxg-eval-api-dev.azurewebsites.net",
        api_client_id="YOUR_API_CLIENT_ID"  # From App Registration
    )
    
    response = client.call_api("api/v1/eval/runs")
    
    if response.status_code == 200:
        return func.HttpResponse(response.text, status_code=200)
    else:
  return func.HttpResponse(
            f"API call failed: {response.status_code}",
            status_code=response.status_code
 )
```

---

### **Step 4: Verify App-to-App Access**

Test the managed identity authentication:

```bash
# From Azure Function, Logic App, or Container App
# The DefaultAzureCredential will automatically use the managed identity

# Test token acquisition (using Azure CLI with managed identity)
az account get-access-token \
  --resource "api://YOUR_API_CLIENT_ID" \
  --query accessToken -o tsv

# This should return a valid access token if properly configured
```

---

## ?? **Flow 2: Delegated User Authentication (OAuth 2.0)**

### **Use Case**
Web applications, desktop applications, or mobile applications where users sign in and grant consent to access the API on their behalf.

---

### **Step 1: Register Client Application**

The client application (web app, desktop app, etc.) needs its own App Registration.

#### **Option A: Azure Portal**

1. Go to **Azure Portal** ? **Azure Active Directory** ? **App registrations**
2. Click **New registration**
3. Fill in:
   - **Name**: `SXG-EvalPlatform-WebClient` (or your app name)
   - **Supported account types**: 
     - **Accounts in this organizational directory only** (single-tenant)
     - OR **Accounts in any organizational directory** (multi-tenant)
- **Redirect URI**: 
     - Platform: **Web** (for web apps) or **Public client** (for desktop/mobile)
     - URI: `https://your-web-app.com/signin-oidc` (your callback URL)
4. Click **Register**
5. **Copy the Application (client) ID** - You'll need this

#### **Option B: PowerShell**

```powershell
Connect-MgGraph -Scopes "Application.ReadWrite.All"

# Create client app registration
$clientApp = New-MgApplication -DisplayName "SXG-EvalPlatform-WebClient" `
  -SignInAudience "AzureADMyOrg" `  # Single-tenant
  -Web @{
        RedirectUris = @("https://your-web-app.com/signin-oidc")
    }

$clientId = $clientApp.AppId
Write-Host "Client App ID: $clientId"
```

---

### **Step 2: Configure API Permissions**

The client application needs permission to access the API.

#### **Option A: Azure Portal**

1. In your **client app registration** (`SXG-EvalPlatform-WebClient`)
2. Go to **API permissions** (left menu)
3. Click **Add a permission**
4. Click **APIs my organization uses** tab
5. Search for: `SXG-EvalPlatform-API-Dev` (your API)
6. Click on it
7. Select **Delegated permissions**
8. Check: `Evaluations.ReadWrite`
9. Click **Add permissions**
10. **(Optional)** Click **Grant admin consent for [Your Organization]**
    - Required if users cannot consent themselves
    - Requires admin privileges

#### **Option B: PowerShell**

```powershell
Connect-MgGraph -Scopes "Application.ReadWrite.All"

# Get your API app
$apiApp = Get-MgServicePrincipal -Filter "displayName eq 'SXG-EvalPlatform-API-Dev'"

# Get the OAuth scope ID for Evaluations.ReadWrite
$scope = $apiApp.Oauth2PermissionScopes | Where-Object { $_.Value -eq "Evaluations.ReadWrite" }
$scopeId = $scope.Id

# Get your client app
$clientApp = Get-MgApplication -Filter "displayName eq 'SXG-EvalPlatform-WebClient'"

# Add API permission
$requiredResourceAccess = @{
    ResourceAppId = $apiApp.AppId  # API's App ID
    ResourceAccess = @(
  @{
        Id = $scopeId  # Scope ID
            Type = "Scope"  # Delegated permission
        }
    )
}

Update-MgApplication -ApplicationId $clientApp.Id `
    -RequiredResourceAccess @($requiredResourceAccess)

Write-Host "? API permission added successfully!"
```

---

### **Step 3: Create Client Secret (for Web Apps)**

If your client is a **web application** (confidential client), you need a client secret.

#### **Option A: Azure Portal**

1. In your **client app registration**
2. Go to **Certificates & secrets**
3. Click **New client secret**
4. Description: `API Access Secret`
5. Expires: Choose expiration (12 months, 24 months, custom)
6. Click **Add**
7. **Copy the secret value** immediately (you won't see it again!)

#### **Option B: PowerShell**

```powershell
Connect-MgGraph -Scopes "Application.ReadWrite.All"

# Get client app
$clientApp = Get-MgApplication -Filter "displayName eq 'SXG-EvalPlatform-WebClient'"

# Add client secret
$passwordCredential = Add-MgApplicationPassword -ApplicationId $clientApp.Id `
    -PasswordCredential @{
        DisplayName = "API Access Secret"
   EndDateTime = (Get-Date).AddMonths(12)  # 12 months expiration
    }

$clientSecret = $passwordCredential.SecretText
Write-Host "Client Secret: $clientSecret"
Write-Host "?? Save this secret now - you won't see it again!"
```

---

### **Step 4: Configure Client Application Code**

#### **For ASP.NET Core Web App**:

**Install NuGet Packages**:
```bash
dotnet add package Microsoft.Identity.Web
dotnet add package Microsoft.Identity.Web.UI
```

**Update `appsettings.json`**:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "YOUR_TENANT_ID",
 "ClientId": "YOUR_CLIENT_APP_ID",
    "ClientSecret": "YOUR_CLIENT_SECRET",
    "CallbackPath": "/signin-oidc",
    "Scopes": "api://YOUR_API_CLIENT_ID/Evaluations.ReadWrite"
  },
  "EvalPlatformApi": {
    "BaseUrl": "https://sxg-eval-api-dev.azurewebsites.net",
    "Scope": "api://YOUR_API_CLIENT_ID/Evaluations.ReadWrite"
  }
}
```

**Update `Program.cs`**:
```csharp
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add Azure AD authentication
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"))
    .EnableTokenAcquisitionToCallDownstreamApi()
    .AddInMemoryTokenCaches();

// Add authorization
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = options.DefaultPolicy;
});

// Add HTTP client for API calls
builder.Services.AddHttpClient("EvalPlatformApi", client =>
{
  client.BaseAddress = new Uri(builder.Configuration["EvalPlatformApi:BaseUrl"]);
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages().AddMicrosoftIdentityUI();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
```

**Create API Client Service**:
```csharp
using Microsoft.Identity.Web;
using System.Net.Http.Headers;

public class EvalPlatformApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly IConfiguration _configuration;

    public EvalPlatformApiClient(
        IHttpClientFactory httpClientFactory,
   ITokenAcquisition tokenAcquisition,
    IConfiguration configuration)
    {
     _httpClientFactory = httpClientFactory;
        _tokenAcquisition = tokenAcquisition;
  _configuration = configuration;
    }

    public async Task<List<EvalRun>> GetEvalRunsAsync()
    {
    // Get access token on behalf of signed-in user
      var scope = _configuration["EvalPlatformApi:Scope"];
        var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
       new[] { scope }
      );

        // Create HTTP client
        var httpClient = _httpClientFactory.CreateClient("EvalPlatformApi");
        httpClient.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", accessToken);

        // Call API
        var response = await httpClient.GetAsync("api/v1/eval/runs");
        response.EnsureSuccessStatusCode();

        var evalRuns = await response.Content.ReadFromJsonAsync<List<EvalRun>>();
        return evalRuns;
    }

    public async Task<EvalRun> CreateEvalRunAsync(CreateEvalRunDto request)
    {
        var scope = _configuration["EvalPlatformApi:Scope"];
        var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(
        new[] { scope }
        );

        var httpClient = _httpClientFactory.CreateClient("EvalPlatformApi");
        httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", accessToken);

   var response = await httpClient.PostAsJsonAsync("api/v1/eval/runs", request);
        response.EnsureSuccessStatusCode();

        var evalRun = await response.Content.ReadFromJsonAsync<EvalRun>();
        return evalRun;
    }
}
```

**Register Service**:
```csharp
// In Program.cs
builder.Services.AddScoped<EvalPlatformApiClient>();
```

**Use in Controller**:
```csharp
[Authorize]
public class EvalRunsController : Controller
{
    private readonly EvalPlatformApiClient _apiClient;

    public EvalRunsController(EvalPlatformApiClient apiClient)
    {
        _apiClient = apiClient;
    }

 public async Task<IActionResult> Index()
    {
    var evalRuns = await _apiClient.GetEvalRunsAsync();
        return View(evalRuns);
 }

    [HttpPost]
    public async Task<IActionResult> Create(CreateEvalRunDto request)
    {
        var evalRun = await _apiClient.CreateEvalRunAsync(request);
        return RedirectToAction(nameof(Index));
    }
}
```

#### **For JavaScript/React Single Page Application (SPA)**:

**Install MSAL**:
```bash
npm install @azure/msal-browser @azure/msal-react
```

**Configure MSAL** (`authConfig.js`):
```javascript
import { LogLevel, PublicClientApplication } from "@azure/msal-browser";

export const msalConfig = {
  auth: {
  clientId: "YOUR_CLIENT_APP_ID",
        authority: "https://login.microsoftonline.com/YOUR_TENANT_ID",
        redirectUri: window.location.origin,
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false,
    },
    system: {
        loggerOptions: {
   loggerCallback: (level, message, containsPii) => {
     if (containsPii) return;
    console.log(message);
  },
      logLevel: LogLevel.Info,
      piiLoggingEnabled: false,
        },
    },
};

export const loginRequest = {
  scopes: ["api://YOUR_API_CLIENT_ID/Evaluations.ReadWrite"]
};

export const msalInstance = new PublicClientApplication(msalConfig);
```

**Initialize MSAL** (`index.js`):
```javascript
import { MsalProvider } from "@azure/msal-react";
import { msalInstance } from "./authConfig";

root.render(
    <MsalProvider instance={msalInstance}>
        <App />
    </MsalProvider>
);
```

**API Client** (`evalPlatformApi.js`):
```javascript
import { msalInstance, loginRequest } from "./authConfig";

const API_BASE_URL = "https://sxg-eval-api-dev.azurewebsites.net";

async function getAccessToken() {
    const accounts = msalInstance.getAllAccounts();
    if (accounts.length === 0) {
        throw new Error("No active account. Please sign in.");
    }

    const request = {
        ...loginRequest,
    account: accounts[0],
    };

    try {
        const response = await msalInstance.acquireTokenSilent(request);
        return response.accessToken;
    } catch (error) {
        // Token acquisition failed, try interactive
        const response = await msalInstance.acquireTokenPopup(request);
        return response.accessToken;
    }
}

export async function getEvalRuns() {
    const token = await getAccessToken();

    const response = await fetch(`${API_BASE_URL}/api/v1/eval/runs`, {
        headers: {
            "Authorization": `Bearer ${token}`,
  "Content-Type": "application/json",
   },
    });

    if (!response.ok) {
        throw new Error(`API call failed: ${response.statusText}`);
    }

    return await response.json();
}

export async function createEvalRun(request) {
    const token = await getAccessToken();

    const response = await fetch(`${API_BASE_URL}/api/v1/eval/runs`, {
  method: "POST",
        headers: {
        "Authorization": `Bearer ${token}`,
            "Content-Type": "application/json",
        },
body: JSON.stringify(request),
    });

    if (!response.ok) {
        throw new Error(`API call failed: ${response.statusText}`);
    }

    return await response.json();
}
```

**Use in Component**:
```javascript
import { useIsAuthenticated, useMsal } from "@azure/msal-react";
import { getEvalRuns, createEvalRun } from "./evalPlatformApi";

function EvalRunsList() {
 const isAuthenticated = useIsAuthenticated();
    const { instance } = useMsal();
    const [evalRuns, setEvalRuns] = useState([]);

    useEffect(() => {
     if (isAuthenticated) {
            loadEvalRuns();
        }
    }, [isAuthenticated]);

    const loadEvalRuns = async () => {
        try {
  const runs = await getEvalRuns();
          setEvalRuns(runs);
   } catch (error) {
    console.error("Failed to load eval runs:", error);
        }
    };

    const handleSignIn = () => {
     instance.loginPopup(loginRequest);
    };

    if (!isAuthenticated) {
  return (
            <div>
       <h2>Please sign in to access the Evaluation Platform</h2>
            <button onClick={handleSignIn}>Sign In</button>
          </div>
   );
    }

    return (
        <div>
       <h2>Evaluation Runs</h2>
            <ul>
                {evalRuns.map(run => (
     <li key={run.evalRunId}>{run.evalRunName}</li>
 ))}
    </ul>
        </div>
  );
}
```

---

### **Step 5: Grant Admin Consent (If Required)**

If your organization requires admin consent for delegated permissions:

#### **Azure Portal**:

1. Go to **Azure Active Directory** ? **App registrations**
2. Find your **client app** (`SXG-EvalPlatform-WebClient`)
3. Go to **API permissions**
4. Click **Grant admin consent for [Your Organization]**
5. Confirm

#### **Direct Consent URL**:

```
https://login.microsoftonline.com/YOUR_TENANT_ID/adminconsent?client_id=YOUR_CLIENT_APP_ID
```

Share this URL with your Azure AD administrator.

---

## ?? **Testing**

### **Test App-to-App Flow**:

```bash
# From Azure Function/Container App with managed identity
az account get-access-token \
  --resource "api://YOUR_API_CLIENT_ID" \
  --query accessToken -o tsv

# Use the token to call API
TOKEN="<token from above>"
curl -H "Authorization: Bearer $TOKEN" \
  https://sxg-eval-api-dev.azurewebsites.net/api/v1/eval/runs
```

### **Test Delegated User Flow**:

1. **Sign in** to your client application
2. **Consent** to permissions (if prompted)
3. **Call API** - should return data
4. **Check token** in browser dev tools (Network tab)

---

## ?? **Summary**

| Flow | Client Type | Authentication | Permission | Code Example |
|------|-------------|----------------|------------|--------------|
| **App-to-App** | Azure Service (Function, Container App) | Managed Identity | App Role Assignment | `DefaultAzureCredential` |
| **Delegated User** | Web App, SPA, Desktop | User Sign-in + OAuth | API Permission | `ITokenAcquisition` / `MSAL` |

---

## ? **Checklist**

### **App-to-App Flow**:
- [ ] Enable managed identity on client service
- [ ] Assign `EvalPlatform.FullAccess` app role
- [ ] Configure client code with `DefaultAzureCredential`
- [ ] Test token acquisition
- [ ] Call API with bearer token

### **Delegated User Flow**:
- [ ] Create client app registration
- [ ] Add API permissions (`Evaluations.ReadWrite`)
- [ ] Grant admin consent (if required)
- [ ] Create client secret (for web apps)
- [ ] Configure client code with MSAL
- [ ] Test user sign-in and consent
- [ ] Call API on behalf of user

---

**Created**: January 2025  
**Status**: ? Complete  
**Next Steps**: Start onboarding your first client application!
