export interface LoginCommandResponse {
  authToken: string;
  fullName: string;
  email: string;
}

export interface PasswordPolicyResponse {
  RequiredLength: number;
  RequiredUniqueChars: number;
  RequireNonAlphanumeric: boolean;
  RequireLowercase: boolean;
  RequireUppercase: boolean;
  RequireDigit: boolean;
}

export interface SendOtpEmailCommandResponse {
  Message: string;
}

export interface VerifyOtpCommandResponse {
  Message: string;
}

export interface ResetPasswordCommandResponse {
  Message: string;
}

export interface SendOtpToCurrentUserCommandResponse {
  Message: string;
}

export interface ChangeCurrentUserPasswordCommandResponse {
  Message: string;
}
