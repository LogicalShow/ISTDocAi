import { Directive, ElementRef, Renderer2, effect, inject, input } from '@angular/core';
import { PermissionService } from '../services/permission.service';
import { PERMISSION_STRATEGIES_CONFIG, PermissionStrategy } from '../config/permission-strategies.config';

/**
 * Attribute directive to Show/Hide or Enable/Disable elements based on permissions.
 *
 * Usage:
 * <button [appPermission]="'MD.BP.AGT.ADD'">Add</button>
 * <button [appPermission]="'MD.BP.AGT.EDIT'" permissionAction="disable">Edit</button>
 */
@Directive({
    selector: '[appPermission]'
})
export class PermissionControlDirective {
    private el = inject(ElementRef);
    private renderer = inject(Renderer2);
    private permissionService = inject(PermissionService);

    readonly permission = input<string | string[]>('', { alias: "appPermission" });
    readonly permissionAction = input<PermissionStrategy>(); // Optional override
    readonly permissionMode = input<'any' | 'all'>('any');

    constructor() {
        // Re-evaluates whenever the inputs or the user's permissions (a signal) change,
        // so live permission updates show/hide elements without a page reload.
        effect(() => this.updateView());
    }

    private updateView(): void {
        const hasPermission = this.checkPermission();

        if (!hasPermission) {
            this.applyStrategy();
        } else {
            // Ensure element is visible/enabled if permission is granted
            // (Restoring state might be complex if it was hidden/disabled by something else,
            // but for now we assume this directive controls it)
            this.renderer.removeStyle(this.el.nativeElement, 'display');
            this.renderer.removeAttribute(this.el.nativeElement, 'disabled');
            this.renderer.removeClass(this.el.nativeElement, 'disabled-by-permission');
        }
    }

    private checkPermission(): boolean {
        const permission = this.permission();
        if (!permission) {
            return true; // No permission required
        }

        if (Array.isArray(permission)) {
            if (this.permissionMode() === 'all') {
                return this.permissionService.hasAllPermissions(permission);
            } else {
                return this.permissionService.hasAnyPermission(permission);
            }
        } else {
            return this.permissionService.hasPermission(permission);
        }
    }

    private applyStrategy(): void {
        // Determine strategy: Input override -> Config override (not implemented yet for specific IDs) -> Default
        const strategy = this.permissionAction() || PERMISSION_STRATEGIES_CONFIG.defaultStrategy;

        if (strategy === 'disable') {
            this.renderer.setProperty(this.el.nativeElement, 'disabled', true);
            this.renderer.setAttribute(this.el.nativeElement, 'disabled', 'true');
            this.renderer.addClass(this.el.nativeElement, 'disabled-by-permission');
        } else {
            // Default: hide
            this.renderer.setStyle(this.el.nativeElement, 'display', 'none');
        }
    }
}
