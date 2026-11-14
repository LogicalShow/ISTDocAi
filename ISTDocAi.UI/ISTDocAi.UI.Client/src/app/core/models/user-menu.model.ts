export interface UserMenuDto {
  Modules: MenuModuleDto[];
}

export interface MenuModuleDto {
  Id: string;
  NameEn: string;
  NameAr: string;
  Icon: string;
  DisplayOrder: number;
  Route?: string | null;
  OpenInNewTab: boolean;
  SubModules: MenuModuleDto[];
  Pages: MenuPageDto[];
}

export interface MenuPageDto {
  Id: string;
  NameEn: string;
  NameAr: string;
  Route: string;
  OpenInNewTab: boolean;
  DisplayOrder: number;
}
