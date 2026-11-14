# Backend URL Configuration

## Overview
The application uses a dynamic configuration system to handle different backend URLs for development and production environments.

## Configuration Files

### Development (`config.json`)
Located at: `src/assets/config.json`
```json
{
  "apiUrl": "https://localhost:7240",
  "production": false
}
```

### Production (`config.prod.json`)
Located at: `src/assets/config.prod.json`
```json
{
  "apiUrl": "",
  "production": true
}
```

## How It Works

1. **ConfigService** loads the configuration file at application startup using `APP_INITIALIZER`
2. Components inject `ConfigService` to get the backend URL dynamically
3. For production, set `apiUrl` to empty string `""` to use relative URLs (same domain as frontend)
4. For different production servers, set `apiUrl` to the full backend URL (e.g., `"https://api.yourserver.com"`)

## Production Deployment

### Option 1: Same Domain (Recommended)
If frontend and backend are on the same server:
- Keep `apiUrl` as `""` in `config.prod.json`
- The app will use relative URLs like `/fastreport/invoice`

### Option 2: Different Domain
If backend is on a different server:
- Update `config.prod.json`:
  ```json
  {
    "apiUrl": "https://your-backend-server.com",
    "production": true
  }
  ```

### Build Commands
```bash
# Development build (uses config.json)
ng build

# Production build
ng build --configuration production

# After build, update dist/assets/config.json with production values
```

## Usage in Components

```typescript
import { ConfigService } from './core/services/config.service';

export class MyComponent {
  constructor(private configService: ConfigService) {}

  openReport(id: string): void {
    const baseUrl = this.configService.apiUrl;
    const url = `${baseUrl}/fastreport/invoice?id=${id}`;
    window.open(url, '_blank');
  }
}
```

## Notes
- The configuration is loaded **before** the Angular app bootstraps
- All FastReport URLs now use the dynamic configuration
- No code changes needed when deploying to different environments
