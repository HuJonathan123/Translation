# Translator Backend

FastAPI backend for the desktop translator. It calls Google Cloud Translation API v3.

## Local run

```powershell
Set-Location backend
pip install -r requirements.txt
$env:GOOGLE_APPLICATION_CREDENTIALS_JSON = Get-Content -Raw C:\path\to\service-account.json
uvicorn app.main:app --reload
```

Verify the service:

```powershell
Invoke-RestMethod http://127.0.0.1:8000/health
Invoke-RestMethod http://127.0.0.1:8000/api/translate -Method Post -ContentType application/json -Body '{"text":"Hello","source_lang":"en","target_lang":"zh-TW"}'
```

## Render deployment

1. Push this project to a GitHub repository.
2. In Render, select **New > Blueprint**, connect the repository, and approve `render.yaml`.
3. In the service's **Environment** settings, add the secret `GOOGLE_APPLICATION_CREDENTIALS_JSON`. Its value must be the complete contents of a Google Cloud service-account JSON key.
4. Enable **Cloud Translation API** in that Google Cloud project, then redeploy.
5. Verify `https://<your-service>.onrender.com/health`, then call `POST /api/translate`.

Do not commit the Google service-account JSON file.
