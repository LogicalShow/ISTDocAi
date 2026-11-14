import { Component, OnInit, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PwaUpdateService } from './core/services/pwa-update.service';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit {
  private pwaUpdateService = inject(PwaUpdateService);
  private themeService = inject(ThemeService);

  ngOnInit(): void {
    this.themeService.init();
    this.pwaUpdateService.init();
  }
  
}
