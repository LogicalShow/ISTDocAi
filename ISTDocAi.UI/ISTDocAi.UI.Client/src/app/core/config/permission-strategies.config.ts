
export type PermissionStrategy = 'hide' | 'disable';

export const PERMISSION_STRATEGIES_CONFIG = {
    defaultStrategy: 'hide' as PermissionStrategy,
    // Define overrides for specific permissions if needed
    // 'MD.BP.AGT.EDIT': 'disable',
};
