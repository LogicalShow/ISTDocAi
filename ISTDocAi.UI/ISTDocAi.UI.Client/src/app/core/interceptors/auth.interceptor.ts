import { HttpInterceptorFn } from '@angular/common/http';
import { HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { Observable } from 'rxjs';
import { inject } from '@angular/core';
import { AppStore } from '../state/app.store';

const authTokenName = 'authToken';

// Functional interceptor for Angular standalone API
export const AuthInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
): Observable<HttpEvent<unknown>> => {
  const appStore = inject(AppStore);
  
  // Try to get the token from localStorage or sessionStorage
  const token = localStorage.getItem(authTokenName) || sessionStorage.getItem(authTokenName);
  
  // Get the current connection ID from AppStore
  const connectionId = appStore.getState().connectionId;

  // Prepare headers object
  const headers: Record<string, string> = {};
  
  if (token) {
   headers['Authorization'] = `Bearer ${token}`;
  }
  
  if (connectionId) {
    headers['X-SignalR-ConnectionId'] = connectionId;
  }

  // Clone the request and add the headers
  const authReq = req.clone({
    setHeaders: headers
  });
  
  return next(authReq);
};
