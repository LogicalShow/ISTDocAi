import { FormGroup } from '@angular/forms';

interface ValidationProblemDetails {
  errors?: Record<string, string[]>;
  title?: string;
  detail?: string;
}

interface ValidationHttpError {
  error?: ValidationProblemDetails;
}

/* Maps all server-side validation errors to their matching form controls. */
export function bindServerErrors(form: FormGroup, err: ValidationHttpError): void {
  const errors = err?.error?.errors as Record<string, string[]> | undefined;
  if (errors && Object.keys(errors).length) {

    //  loop through all keys, not just the first one
    for (const [ctrlName, msgs] of Object.entries(errors)) {
      //  join multiple messages into one string  , each error per line. 
      const msg = msgs?.join('\n') || 'Invalid value';
      const ctrl = form.get(ctrlName);

      if (ctrl) {
        ctrl.setErrors({ ...(ctrl.errors || {}), server: msg });
        ctrl.markAsTouched();
      } else {
        form.setErrors({ ...(form.errors || {}), server: msg });
      }
    }

  } else {
    const msg = err?.error?.title || err?.error?.detail || 'An error occurred';
    form.setErrors({ ...(form.errors || {}), server: msg });
  }
}
 
/* Clears any server-side validation error from a specific control. */
export function clearServerError(form: FormGroup, controlName: string): void {
  /* "server" key stores backend (API) validation errors shown with client-side ones. */
  const ctrl = form.get(controlName);
  if (ctrl?.hasError('server')) {
    const errors = { ...ctrl.errors };
    delete errors['server'];

    ctrl.setErrors(Object.keys(errors).length ? errors : null);

    ctrl.updateValueAndValidity({ emitEvent: false });

  }
}
