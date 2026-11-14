/**
 * Route to Permission Mapping
 *
 * This configuration maps all application route paths to their required permissions.
 * It enables dynamic permission checking through the PermissionGuard without
 * repeating permission configuration in every route definition.
 *
 * Format: 'full/route/path': PERMISSIONS.PAGE.VIEW
 *
 * IMPORTANT: Route paths should NOT include the leading slash (/)
 * Example: 'iam/roles' not '/iam/roles'
 *
 * How this works:
 * 1. Routes are protected using PermissionGuard in canActivate
 * 2. PermissionGuard looks up the route path in this configuration
 * 3. If a permission is found, it checks if the user has that permission
 * 4. Access is granted only if the user has the required permission
 */

import { PERMISSIONS } from '../constants/permissions.constants';

export const ROUTE_PERMISSIONS: Record<string, string> = {
  'iam/roles': PERMISSIONS.ROLE.VIEW,
  'iam/roles/permissions/:id': PERMISSIONS.ROLE.EDIT,
  'iam/users': PERMISSIONS.USER.VIEW,
  'iam/users/permissions/:id': PERMISSIONS.USER.EDIT,
  'public/setting': PERMISSIONS.SETTING.VIEW,
  'public/notificationSettings': PERMISSIONS.NOTIFICATION.VIEW
};
