# Faro Alerting Engine

## Configuration

The Alerting Engine requires notification channel credentials to send alerts. These sensitive values should NOT be committed to Git.

### Setting up User Secrets (Recommended for Development)

Use .NET User Secrets to store sensitive configuration locally:

```bash
cd src/Faro.AlertingEngine

# Email notification settings
dotnet user-secrets set "Notifications:Channels:email:Username" "your-email@gmail.com"
dotnet user-secrets set "Notifications:Channels:email:Password" "your-app-password"
dotnet user-secrets set "Notifications:Channels:email:FromAddress" "your-email@gmail.com"
dotnet user-secrets set "Notifications:Channels:email:ToAddresses:0" "recipient@example.com"

# Webhook notification settings
dotnet user-secrets set "Notifications:Channels:webhook:Url" "https://your-webhook-url.com"
```

To view all secrets:
```bash
dotnet user-secrets list
```

### Alternative: appsettings.Development.json

You can also create an `appsettings.Development.json` file (already gitignored) based on the `.example` file:

```bash
cp appsettings.Development.json.example appsettings.Development.json
# Edit appsettings.Development.json with your actual values
```

### Gmail App Password

For Gmail SMTP, you need to:
1. Enable 2-factor authentication on your Google account
2. Generate an App Password at https://myaccount.google.com/apppasswords
3. Use the app password (not your regular password) in the configuration

### Production Configuration

For production deployments, use environment variables or a secure secret management service:

```bash
# Environment variables
export Notifications__Channels__email__Username="your-email@gmail.com"
export Notifications__Channels__email__Password="your-app-password"
export Notifications__Channels__webhook__Url="https://your-webhook-url.com"
```

## Running the Application

```bash
dotnet run
```

The application will merge settings from:
1. `appsettings.json` (base configuration)
2. User Secrets (development secrets)
3. `appsettings.Development.json` (if it exists)
4. Environment variables (highest priority)
