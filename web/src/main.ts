import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { appConfig } from './app/app.config';
import { ThemeService } from './app/core/theme.service';

// Apply the cached theme before the first render to avoid a light/dark flash.
ThemeService.applyCached();

bootstrapApplication(App, appConfig).catch((err) => console.error(err));
