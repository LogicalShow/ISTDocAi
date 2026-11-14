export const PERMISSIONS = {
  ROLE: {
    VIEW: 'ADM.IAM.ROL.VIEW',
    ADD: 'ADM.IAM.ROL.ADD',
    EDIT: 'ADM.IAM.ROL.EDIT',
    DELETE: 'ADM.IAM.ROL.DELETE',
    PRINT: 'ADM.IAM.ROL.PRINT'
  },
  USER: {
    VIEW: 'ADM.IAM.USR.VIEW',
    ADD: 'ADM.IAM.USR.ADD',
    EDIT: 'ADM.IAM.USR.EDIT',
    DELETE: 'ADM.IAM.USR.DELETE',
    PRINT: 'ADM.IAM.USR.PRINT'
  },
  SETTING: {
    VIEW: 'ADM.SET.VIEW',
    EDIT: 'ADM.SET.EDIT'
  },
  NOTIFICATION: {
    VIEW: 'ADM.NOT.VIEW',
    ADD: 'ADM.NOT.ADD',
    EDIT: 'ADM.NOT.EDIT',
    DELETE: 'ADM.NOT.DELETE'
  }
} as const;

export type PermissionKey = string;
