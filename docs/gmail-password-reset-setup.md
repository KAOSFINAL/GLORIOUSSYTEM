# Gmail SMTP password reset setup

The app now uses Gmail SMTP to deliver a six-digit, one-time password-reset code. The API sends through `smtp.gmail.com` on port `587` with STARTTLS. Codes expire after 15 minutes, and resetting a password revokes existing API sessions.

## Create a Gmail app password

1. Sign in to the Gmail/Google account that will send system email.
2. Turn on Google 2-Step Verification.
3. Open Google Account → Security → App passwords.
4. Create an app password for this API and copy the generated 16-character value.

Use that app password—not the normal Gmail password. If Google does not show App passwords, the account policy may not allow them; use an approved SMTP relay instead.

## Run the local API from Windows CMD

Open a new **Command Prompt** window and run these commands. Replace the two placeholders with the sender Gmail address and its app password.

```cmd
cd /d C:\Dev\GLORIOUSSYSTEM
set "Email__SmtpUser=your-account@gmail.com"
set "Email__SmtpPassword=your-16-character-app-password"
set "Email__FromAddress=your-account@gmail.com"
set "Email__FromName=GLORIOUS SYSTEM"
dotnet run --project src\GLORIOUSSYSTEM.Api\GLORIOUSSYSTEM.Api.csproj
```

The development launch profile uses `http://localhost:5053`, which is the default Windows app address. The Windows app can also start the local API automatically; make sure the environment variables above are set before opening the app so the child API process inherits them.

For Android, use the computer's LAN address in the app's Settings → Sensor API address (for example, `http://192.168.1.10:5053/`) and allow the port through Windows Firewall. A physical Android device cannot reach `localhost`; the Android emulator uses `http://10.0.2.2:5053/`.

## Security notes

- Do not put `Email__SmtpUser` or `Email__SmtpPassword` in `appsettings.json`, source code, or GitHub.
- These `set` values last only for the current Command Prompt window. Close the window to clear them, or use a secret manager for deployment.
- Production should use HTTPS and a secret manager/environment variables for the SMTP password, API key, and session signing key.
- The reset request intentionally returns a generic message so it does not reveal whether an email address has an account.
